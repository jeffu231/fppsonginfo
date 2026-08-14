# ExecPlan: Direct MRDS192 RDS Updating

## Objective

Add direct MiniRDS/MRDS192 support over a bidirectional COM adapter while preserving reliable file output. MQTT sources will publish only complete `SongInfo` values to a composite output coordinator; the file sink and a single-owner RDS hosted worker will consume updates independently.

MRDS1322 and its `0xFE ... 0xFF` UART framing are explicitly out of scope. MRDS192 communication is native I2C emulation over serial modem-control lines: DTR is SDA output, CTS is SDA input/ACK, and RTS is SCL. The electrical polarity remains an implementation detail of the transport and must be verified with the deployed adapter.

## Architecture decisions

- Replace the source-facing dependency on `ISongInfoWriter` with `ISongInfoPublisher`. `SongInfoPublisher` is a singleton composite that serializes publication with `SemaphoreSlim`, invokes each enabled `ISongInfoSink`, and logs a structured error for an individual sink failure without suppressing the other sinks.
- Extract the existing `SongInfoWriter` behavior into `FileSongInfoSink : ISongInfoSink`. Preserve its current atomic-write/compatible in-place fallback behavior, but make it conditional on `OutputOptions.Enabled` (default `true`).
- Implement `RdsUpdaterService : BackgroundService, ISongInfoSink` as the exclusive owner of an MRDS192 device client and COM transport. It receives updates via a bounded, capacity-one `Channel<SongInfo>` with `DropOldest`; it alone performs all serial I/O and RadioText scheduling.
- Separate protocol mechanics from device-register behavior: `SerialControlLineMrds192Bus` bit-bangs register I2C operations through `IMrds192Bus`; `Mrds192DeviceClient` expresses MRDS192 initialization, PS/DPS, and RadioText register sequences through `IMrds192DeviceClient`.
- Use one RDS worker event loop and injected `TimeProvider`. It waits for either the newest queued song or the next RadioText deadline using `Task.WhenAny`; it must not use overlapping timer callbacks.
- Keep configuration RAM-only. The RDS code may own only registers `0x02-0x09`, `0x1F`, `0x20-0x5F`, `0x70`, and `0x72-0xBE`; it must never write control/EEPROM register `0x45`, PI, PTY, TP, TA, synchronization, phase, or other transmitter configuration.
- Keep concrete new production types `internal sealed` unless inheritance is required. Keep the contracts internal; if a public API is introduced, give it XML documentation.

## Implementation steps

### 1. Add packages, RDS/output configuration, and conditional validation

Update `FPPSongInfo/FPPSongInfo.csproj` to reference the .NET 10-compatible `System.IO.Ports` package. Add the following configuration types under `FPPSongInfo/Configuration/`:

- `DynamicPsMode` with values `FixedEightCharacters = 0`, `CharacterScrolling = 1`, `WordAligned = 2`, and `SpaceSeparatedScrolling = 3`.
- `RdsOptions : IValidatableObject`, section name `Rds`, with `Enabled = false`, `PortName`, `Slow = false`, `StaticProgramService = "LTSHOW"`, `DynamicProgramService = "Compound Radio"`, `DynamicPsMode = SpaceSeparatedScrolling`, `LabelPeriod = 00:00:02.500`, `LoopDelay = 00:00:05.400`, `RadioTextRotationInterval = 00:00:30`, and an additional-message collection containing `lightshow.onthecompound.org` in the sample configuration.
- `OutputOptions.Enabled`, defaulting to `true`. Change its existing file path/name validation so it runs only when output is enabled.

Validate `RdsOptions` only as necessary when RDS is enabled: require a nonblank port; normalize static PS and dynamic PS input for subsequent use; accept static PS from 1 through 8 normalized characters and dynamic PS from 1 through 72; restrict the enum to 0-3; restrict label/loop durations to the device’s 0-255 register range; require a positive RadioText interval; permit at most three supplemental messages; and limit each encoded RadioText message to 64 characters. Dynamic PS whitespace is normalized by splitting on all whitespace and joining with one space, so `Compound  Radio` becomes `Compound Radio`.

Bind and validate the new options at startup in `ConfigureServiceCollectionExtension.cs`; add disabled RDS defaults and `Output.Enabled` to `appsettings.json`. Disabled RDS must not require a port, and disabled file output must not require a valid path or name.

### 2. Introduce composite output contracts and refactor sources

Create `Service/ISongInfoPublisher.cs` with `PublishAsync(SongInfo songInfo, CancellationToken cancellationToken)` and `Service/ISongInfoSink.cs` with `UpdateAsync(SongInfo songInfo, CancellationToken cancellationToken)`. Add `SongInfoPublisher`, accepting the enabled sink collection and logger, with a publication semaphore and per-sink exception isolation. Log publication success as `published song information`; source consumers should no longer catch file-specific exceptions or claim that they wrote a file.

Rename/refactor `SongInfoWriter` and `ISongInfoWriter` into `FileSongInfoSink`, preserving all file mechanics and making it an `ISongInfoSink`. Register the file sink only when `Output:Enabled` is true.

Update both MQTT consumers to inject `ISongInfoPublisher`:

- `FppConsumerService` retains separate artist/title state. It must publish nothing until it has observed a nonblank artist and a nonblank title. Thereafter, a change to either accepted FPP field publishes the current complete pair. The unavoidable transient old/new pairing from separate, uncorrelated topics remains documented rather than being hidden with invented batching.
- `RadioAutomationConsumerService` must ignore malformed or incomplete payloads. It publishes only when artist and title are both nonblank, ignores album, and sends the complete value through the publisher.
- The publisher’s serial order makes FPP and radio automation equal-priority sources: the most recently accepted complete update wins. The sink-visible output string is exactly `{trimmed artist} - {trimmed title}`.

Update the current test fakes from writer-based to publisher/sink-based equivalents and keep existing MQTT channel/lifecycle behavior intact.

### 3. Build text and timing primitives

Add `Rds/RdsTextEncoder.cs` as the single source of MRDS display-text conversion. It must normalize Unicode to decomposed form, remove combining marks when they have a basic Latin equivalent, collapse whitespace, replace unsupported/nonprintable characters with `?`, and emit the selected basic single-byte RDS-compatible character set. Provide explicit helpers to truncate to encoded-byte limits and pad static PS to eight bytes and RadioText to 64 bytes. Apply limits of 8 for static PS, 72 for DPS, and 64 for RadioText.

Add `Rds/RdsTimingConverter.cs` to convert `TimeSpan` settings into register values using `MidpointRounding.AwayFromZero` and clamps:

```text
labelRaw = clamp(round(LabelPeriod.TotalSeconds / 0.54), 0, 255)
loopDelayRaw = clamp(round(LoopDelay.TotalSeconds / 2.7), 0, 255)
```

Expose effective timing values where useful for diagnostics/tests. Preserve device semantics for loop value 255 (show DPS once after it changes). Confirm 2.5 seconds maps to 5 and 5.4 seconds maps to 2. Scrolling speed is always the low value `0`.

### 4. Implement the modem-line I2C transport

Create `Rds/IMrds192Bus.cs` with cancellation-aware `Open`, `Close`, `Write(registerAddress, data, cancellationToken)`, and `Read(registerAddress, length, cancellationToken)` operations, plus disposal ownership as appropriate. Implement it with `SerialControlLineMrds192Bus`, which owns a `System.IO.Ports.SerialPort` and is responsible for close/dispose after transport failures.

Open `COM` at 19,200 when `Slow` is false and 2,400 when true, but make clear in code/comments that the baud rate does not clock I2C. Select normal versus conservative transition timing profiles from `Slow`, honoring MRDS192 minimum SCL high/low and post-transaction bus-free times. Do not call this transport from MQTT callbacks.

Encapsulate DTR/RTS asserted polarity in small SDA/SCL helpers so the deployed adapter can be validated without leaking polarity assumptions into device code. Implement START, STOP, byte writes, ACK sampling via CTS, byte reads with master ACK/NACK, repeated START, and cancellation checks between bytes and while delaying. An unacknowledged transmitted byte must throw `IOException`.

Use address `0xD6` for writes and `0xD7` for reads. A normal write is `START -> 0xD6 -> register -> data -> STOP`; a random read writes the register pointer, issues a repeated START, sends `0xD7`, reads the requested bytes, then STOP. Ensure every error leaves or restores a safe bus state before the next retry.

### 5. Implement MRDS192 register client and initialization sequence

Create `Rds/IMrds192DeviceClient.cs` and `Rds/Mrds192DeviceClient.cs`. The client owns only MRDS192 register-level operations and delegates transfer framing to `IMrds192Bus`. Define named register constants rather than scattering literals.

On initial connect and every reconnect, perform this sequence in order:

1. Open the bus and read status `0x70` to verify bidirectional communication.
2. Read RTEN (`0x1F`) to capture the existing RadioText A/B state.
3. Write the eight-byte padded static PS to buffered address `0x02`, then wait at least 620 ms before any new transaction.
4. Write SPS/DPS loop delay (`0x72`), DPS mode (`0x73`), label period (`0x74`), and low scrolling speed (`0x75`).
5. Write `DPSNUM` (`0x76`) as zero, write normalized/encoded Dynamic PS at `0x77`, then write its encoded length to `0x76` to restart the loop.
6. Enable RadioText in `0x1F` while preserving the A/B bit, but leave existing RadioText data unchanged until a complete song exists.

Provide explicit device-client operations for that DPS reset/content/restart sequence and for RadioText writes. A RadioText change first writes padded 64-byte text to `0x20`, then writes enabled RTEN with the opposite A/B bit. Commit the in-memory successful A/B state only after both writes succeed.

### 6. Add the single-owner RDS updater and dependency registration

Create `Service/RdsUpdaterService.cs` as both `ISongInfoSink` and `BackgroundService`. `UpdateAsync` should only enqueue/update the newest complete song in its capacity-one `DropOldest` channel; it must never open the port or execute device operations itself. The background worker retains static configuration and the latest song through failures.

After connection, construct RadioText slots as `[trimmed artist + " - " + trimmed title]` plus nonempty configured supplemental messages. Do not rotate before the first complete song. On a song update, replace slot zero, immediately send it, set index zero, and reset the rotation deadline. At each deadline, advance modulo slot count, skip empty supplements, send the selected message, and schedule the next deadline. A new song always cancels/resets the active interval and returns immediately to slot zero.

Use the same worker loop for connection attempts, channel reads, and the next `TimeProvider`-based rotation delay. On I/O/transport failure, close and dispose the failed port, log port/operation/register/retry delay as structured fields, and retry after 1, 2, 5, 10, then 30 seconds (capped). On reconnect, replay all volatile PS/DPS/RT configuration and immediately resend the latest song’s RadioText when one exists. Keep file updates available throughout RDS failure.

In `ConfigureServiceCollectionExtension.cs`, register a single `RdsUpdaterService` singleton, expose that exact same instance as its `ISongInfoSink`, and register it as `IHostedService` without constructing a second instance. Register `FileSongInfoSink` as an `ISongInfoSink` only when file output is enabled. Register RDS transport/client/sink only when RDS is enabled. Inject `TimeProvider.System` through DI unless tests replace it.

### 7. Expand automated coverage with fakes at both boundaries

Extend `FPPSongInfo.Tests` rather than creating another test project. Add fake modem-line/transport and fake device-client test doubles so protocol state and service behavior can be asserted without a COM device. Cover:

- Conditional `OutputOptions`/`RdsOptions` validation, whitespace normalization, message counts and byte limits.
- Timing conversion and clamping, including 2.5-to-5 and 5.4-to-2 mappings.
- Text transliteration/normalization, unsupported-character replacement, encoded truncation, and PS/RT padding.
- I2C START/STOP, write/read addressing, repeated START, ACK/NACK failure, timing/cancellation, and line ownership using fake modem lines.
- Register initialization order; required 620 ms PS delay; DPSNUM zero/content/length restart; only owned volatile registers; and no write to `0x45`.
- Immediate slot-zero RadioText, rotation/wrap/empty-slot skipping, reset on a newer song, and A/B state mutation only after both writes succeed.
- Capacity-one latest-value behavior while disconnected, retry progression, reconnect configuration replay, and immediate latest-song RadioText after recovery.
- Publisher serialization and sink failure isolation, including continued file delivery when RDS fails.
- FPP’s wait-for-both-fields rule and subsequent updates; radio automation rejection of incomplete metadata; exact trimmed `Artist - Title` format.

Use a controllable/fake `TimeProvider` or equivalent deterministic time source so rotation and retry tests do not sleep. Update current writer tests and recording fake types for the new sink/publisher contracts.

### 8. Validate build, configuration, and deployed hardware behavior

Run the full solution restore, build, and tests. Verify that the plan’s package use remains compatible with the repository’s Windows worker target while source compilation stays portable for CI.

For manual acceptance, close TinyRDS before claiming the port, enable both output sinks, then:

1. Start the service and verify static `LTSHOW` followed by scrolling `Compound Radio`.
2. Publish a complete artist/title update and verify RadioText immediately shows `Artist - Title`.
3. Verify RadioText rotates with `lightshow.onthecompound.org` while PS/DPS remains visible.
4. Disconnect and reconnect the COM adapter; verify retry/recovery without restarting and that latest text returns.
5. While disconnected, verify `CurrentSong.txt` still updates.
6. Restart and verify no EEPROM persistence/control command is sent.
7. Confirm actual DTR/RTS/CTS polarity and timing against the deployed bidirectional adapter; adjust only transport-level polarity/timing if required.

## Commit checkpoints

Complete each numbered implementation step as an independently buildable, tested commit.

1. `feat(config): add validated RDS output settings`
2. `refactor(output): publish song updates to independent sinks`
3. `feat(rds): add MRDS text and timing conversion`
4. `feat(rds): implement COM control-line I2C transport`
5. `feat(rds): configure MRDS192 volatile registers`
6. `feat(rds): add resilient RadioText updater service`
7. `test(rds): cover transport device and output behavior`
8. `test: validate direct RDS deployment workflow`

## Acceptance criteria

- Given RDS is disabled, when the host validates configuration, then no COM port is required and file output remains backward-compatible by default.
- Given both file and RDS sinks are enabled, when a complete source update arrives, then every enabled sink receives the same publication, and one sink’s failure does not block the other.
- Given FPP sends artist and title independently, when only one is known or either is blank, then no publication occurs; once both are nonblank, the latest complete pair is published for either later field change.
- Given radio automation metadata lacks a nonblank artist or title, when it is received, then it is ignored.
- Given an MRDS192 connection succeeds, when it initializes, then it writes PS/DPS/RT configuration in the stated order, delays at least 620 ms after buffered PS, and never writes `0x45`.
- Given a complete song reaches the RDS worker, when it is processed, then `Artist - Title` is transmitted immediately as RadioText, followed by configured nonempty supplemental slots at the configured interval.
- Given a RadioText write fails, when the service retries, then the A/B state changes only after a later complete text-and-RTEN write succeeds.
- Given the adapter disconnects, when file and RDS outputs are active, then file output continues, the RDS worker retries with capped backoff, and reconnect replays configuration plus the newest song.

## Validation commands

```powershell
dotnet restore FPPSongInfo.sln
dotnet build FPPSongInfo.sln --no-restore
dotnet test FPPSongInfo.sln --no-build
dotnet publish FPPSongInfo/FPPSongInfo.csproj -p:PublishProfile=FolderProfile.pubxml
```
