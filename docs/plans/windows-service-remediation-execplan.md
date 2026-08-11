# ExecPlan: Windows Service Reliability Remediation

## Objective

Make MQTT processing, song-file output, service lifecycle, configuration, CI, and test coverage deterministic and supportable for Windows-service deployment.

## Architecture decisions

- Use an awaitable MQTT callback plus a per-consumer `Channel<MqttApplicationMessageReceivedEventArgs>` with one reader. This eliminates `async void`, observes failures, and serializes each consumer's state updates.
- Keep one shared `SongInfoWriter`, protected by `SemaphoreSlim`, so FPP and radio updates cannot interleave.
- Prefer a unique temporary file and same-volume replacement; when a third-party reader prevents replacement, use the current compatible in-place write scheme under the writer lock.
- Move MQTT connection ownership into a dedicated hosted service; DI construction only creates/configures the client.
- Make implementation types `internal sealed` where possible, documenting any remaining public API.

## Implementation steps

### 1. Add validated configuration

Add records under `FPPSongInfo/Configuration/`:

- `MqttOptions`: `Enabled`, `Broker`, `Port`, `RootTopic`, `ClientId`
- `FppOptions`: `SongTopic`
- `RadioAutomationOptions`: `SongTopic`
- `OutputOptions`: `FilePath`, `FileName`

Bind each with `AddOptions<T>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()`. Add custom validation for ports, enabled MQTT connection fields, valid output file names, and non-empty topic fragments. Replace all string-key configuration access with typed options and use one helper to normalize and compose MQTT topics.

### 2. Redesign MQTT lifecycle and contract

Update `IMqttClient` to expose an awaitable message callback, cancellation-aware `Task` subscription APIs, `StartAsync`, `StopAsync`, and asynchronous disposal. Remove ambiguous Boolean success results.

Refactor `MqttClient` to avoid synchronously starting in its constructor; track desired subscriptions and replay them after reconnect; await each registered callback while logging callback failures independently; remove managed-client event handlers on shutdown; and dispose the managed client.

Log only safe fields such as enabled state, broker host, port, and client ID. Use `LogError(exception, ...)` for connection failures. Respect `Mqtt:Enabled` by not starting MQTT or registering consumers when disabled.

Add `MqttConnectionHostedService`, registered before consumers, to own asynchronous startup and deterministic stop/disposal.

### 3. Serialize consumer processing

Refactor `FppConsumerService` and `RadioAutomationConsumerService` so that each:

- Registers an awaitable callback that queues messages in an unbounded channel.
- Has exactly one channel reader processing messages in order.
- Registers the configured subscription and relies on reconnect replay.
- Uses `try/finally` to detach its callback, complete the channel, and unregister its subscription even on cancellation.

For FPP, build exact artist/title topics and compare using `StringComparison.Ordinal`. Update state for either field, then write the current artist/title snapshot after every accepted message. Empty FPP payloads clear that field so stale output is not retained.

For radio automation, compare only the exact configured `songinfo` topic. Move `SongInfo` to `Service/SongInfo.cs` as an immutable record. Catch and log `JsonException` for malformed/non-object payloads, ignore empty radio payloads, and allow valid JSON with empty fields to explicitly clear metadata.

### 4. Make file output atomic and observable

Change `ISongInfoWriter` to accept `SongInfo` and a `CancellationToken`. Refactor `SongInfoWriter` to inject `OutputOptions`, null-check dependencies, and be `internal sealed`.

The file is consumed by an uncontrollable third-party application, so the writer cannot impose a reader sharing or watcher contract. Preserve the current proven-compatible behavior as a fallback: opening the destination with `FileMode.Create`, `FileAccess.Write`, and `FileShare.ReadWrite`. This allows cooperating readers that are already compatible with the current service to continue reading the same path without requiring delete sharing.

For each update:

1. Acquire a shared `SemaphoreSlim`.
2. Create the configured directory.
3. Write UTF-8 content to a unique adjacent temp file using asynchronous I/O, `FileAccess.Write`, and `FileShare.None`.
4. Attempt to atomically replace the destination with `File.Move(tempPath, destinationPath, overwrite: true)`.
5. If the replacement fails because the third-party reader does not allow deletion/rename sharing, retry the replacement with bounded, cancellation-aware backoff.
6. If replacement remains blocked, fall back to the current compatible in-place pattern: open the destination using `FileMode.Create`, `FileAccess.Write`, and `FileShare.ReadWrite`, then write and flush the complete UTF-8 content while holding the writer semaphore.
7. Log the fallback at warning level with the path and failure type; log successful atomic replacement at debug level. Remove an uncommitted temp file in `finally` and release the semaphore.

Retry narrow transient `IOException` failures with bounded, cancellation-aware backoff. If both replacement and the compatibility fallback fail, propagate the final failure; consumers catch and log expected output errors with source/topic context. Do not catch every exception in the writer.

### 5. Define lifecycle and failure semantics

- Managed MQTT reconnect handles connection loss; failures are structured-error logged.
- Subscription registration/unregistration either completes or throws; callers log and handle the failure explicitly.
- File writes retry only transient I/O failures, then report failure to the source consumer.
- Propagate cancellation through lifecycle methods, channel reads, retry delays, and writes; cancellation is not an error.
- Rely on `finally`, not post-loop statements, for consumer cleanup.

### 6. Apply structural and documentation standards

- Make concrete services, options, interfaces, helpers, and `SongInfo` internal unless externally consumed.
- Add `InternalsVisibleTo("FPPSongInfo.Tests")`.
- Add XML documentation to remaining public symbols.
- Use consistent primary constructors where clear; otherwise use conventional constructors with `ArgumentNullException.ThrowIfNull`.
- Replace/remove `Console.WriteLine` from `ConfigureServiceCollectionExtension`.
- Remove the duplicate `OutputType` declaration.
- Normalize existing source formatting and UTF-8/LF encoding.

### 7. Enforce CI formatting and standards

Expand `.editorconfig` with C# indentation, braces, spacing, newline, modifier-order, naming, and analyzer-severity settings. Enable build-time code-style analysis and selected style/quality diagnostics as build failures in the project.

In `.github/workflows/build.yml`, before Versionize, run unconditionally:

```powershell
dotnet restore FPPSongInfo.sln --runtime win-x64
dotnet build FPPSongInfo.sln --no-restore
dotnet test FPPSongInfo.sln --no-build
dotnet format FPPSongInfo.sln --verify-no-changes --no-restore
```

Correct publish to:

```powershell
dotnet publish FPPSongInfo/FPPSongInfo.csproj -p:PublishProfile=FolderProfile.pubxml
```

Keep publishing, artifact upload, and release creation conditional on successful Versionize only.

### 8. Add xUnit coverage

Create `FPPSongInfo.Tests`, add it to the solution, and use a test MQTT dispatcher plus temporary output directories. Cover:

- FPP artist/title arrival in either order, including empty field clearing.
- Exact-topic matching and lookalike-topic rejection.
- Valid, malformed, empty, and incomplete radio payloads.
- Concurrent FPP/radio updates: updates are serialized, no temp files remain, and atomic replacement is used when the reader permits it.
- Writer retry and propagation for directory, permission, and replacement failures.
- Atomic replacement while available, plus a third-party-reader sharing violation that exercises the serialized `FileMode.Create`/`FileShare.ReadWrite` compatibility fallback.
- MQTT start, subscription replay after reconnect, unsubscribe, disposal, and callback removal.
- Cancellation while waiting, processing, retrying, and shutting down.
- Invalid options failing host startup.
- Disabled MQTT preventing broker connection and consumer activation.

### 9. Update deployment documentation

Remove environment-specific broker and topic defaults from committed `appsettings.json`; retain only safe placeholders or sample values. Update `README.md` with deployment configuration, including Windows-service environment variables such as `Mqtt__Broker`, `Mqtt__RootTopic`, `FPP__SongTopic`, `RadioAutomation__SongTopic`, and `Output__FilePath`.

## Commit checkpoints

Complete each numbered implementation step as one independently buildable and
tested commit. Use the following paste-ready messages after completing the
corresponding step.

### Step 1

```text
feat(config): validate service options at startup

Bind MQTT, consumer, and output settings to typed options so invalid
deployment configuration fails before the service begins processing.
```

### Step 2

```text
refactor(mqtt): manage connection through hosted lifecycle

Move MQTT startup and disposal out of dependency construction so connection,
reconnect, subscriptions, and shutdown have deterministic async semantics.
```

### Step 3

```text
fix(consumers): serialize MQTT message processing

Replace async-void callbacks with queued single-reader processing to preserve
message state, report failures, and clean up handlers during shutdown.
```

### Step 4

```text
fix(output): serialize compatible song file writes

Prevent concurrent file updates and prefer atomic replacement while retaining
the proven in-place fallback needed for third-party reader compatibility.
```

### Step 5

```text
fix(lifecycle): unify cancellation and failure handling

Make retry, cancellation, subscription, and output failure behavior explicit
so shutdown cleanup and operational logs are reliable.
```

### Step 6

```text
refactor: align service structure with .NET standards

Reduce the public surface and apply immutable models, null validation,
documentation, and project cleanup consistently.
```

### Step 7

```text
ci: enforce build tests and formatting

Run verification independently of release versioning and use portable paths
so Ubuntu release builds reliably validate the entire solution.
```

### Step 8

```text
test: cover service reliability scenarios

Add automated coverage for message ordering, output contention, lifecycle
cleanup, configuration validation, and expected fault handling.
```

### Step 9

```text
docs: document deployment configuration

Remove environment-specific defaults and describe the deployment-provided
settings required to run the Windows service safely.
```

## Acceptance criteria

- Given artist/title messages arrive in either order, when either field changes, then `CurrentSong.txt` reflects the latest complete FPP state.
- Given concurrent source updates, when writes overlap, then updates are serialized; readers see atomic old/new content when replacement is permitted, or the proven-compatible in-place behavior when a third-party lock blocks replacement.
- Given cancellation or shutdown, when hosted services stop, then callbacks are detached, subscriptions are removed, MQTT stops, and resources are disposed.
- Given invalid startup configuration, when the host starts, then validation fails before MQTT connection or file output is attempted.
- Given Versionize finds no release, CI still restores, builds, tests, and verifies formatting.
- Given Versionize creates a release, Ubuntu CI successfully publishes using the portable project path.

## Validation commands

```powershell
dotnet restore FPPSongInfo.sln --runtime win-x64
dotnet build FPPSongInfo.sln --no-restore
dotnet test FPPSongInfo.sln --no-build
dotnet format FPPSongInfo.sln --verify-no-changes --no-restore
dotnet publish FPPSongInfo/FPPSongInfo.csproj -p:PublishProfile=FolderProfile.pubxml
```
