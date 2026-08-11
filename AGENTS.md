# Repository Guidelines

## Project Structure & Module Organization

`FPPSongInfo.sln` is the solution entry point. The application project lives in
`FPPSongInfo/` and targets a Windows .NET worker service. `Program.cs` creates
the host; `ConfigureServiceCollectionExtension.cs` wires configuration into the
service container. Keep MQTT transport code in `FPPSongInfo/Mqtt/` and hosted
consumer and file-writing behavior in `FPPSongInfo/Service/`. Runtime defaults
are in `FPPSongInfo/appsettings.json`; do not commit environment-specific broker
credentials or host details. The sample output under `FPPSongInfo/fpp-media/`
illustrates the generated `CurrentSong.txt` file.

## Build, Test, and Development Commands

- `dotnet restore FPPSongInfo.sln` restores packages.
- `dotnet build FPPSongInfo.sln` compiles the worker service; run this before
  submitting changes. CI verifies an Ubuntu build, so avoid Windows-only build
  assumptions outside the intended service runtime.
- `dotnet run --project FPPSongInfo/FPPSongInfo.csproj` runs the service using
  the local configuration.
- `dotnet publish FPPSongInfo/FPPSongInfo.csproj -p:PublishProfile=FolderProfile.pubxml`
  creates the deployable single-file Windows executable.
- `docker-compose -f docker-compose.yml -f docker-compose-dev.yml up -d`
  builds and runs the development container.

There is currently no automated test project. Add focused tests alongside new
logic when practical, use the framework’s normal `dotnet test` command once a
test project exists, and manually verify MQTT input and the resulting output
file for behavior changes.

## Coding Style & Naming Conventions

Use C# with nullable reference types enabled. Follow existing .NET naming:
PascalCase for public types, methods, and properties; camelCase for local
variables and parameters; interfaces begin with `I` (for example,
`ISongInfoWriter`). Use four-space indentation in new or modified code, keep
one type per file when reasonable, and favor dependency injection over static
service access. No formatter or linter is configured; keep edits consistent
with surrounding code and let `dotnet build` catch compiler and nullable
warnings.

## Commit & Pull Request Guidelines

Recent history uses Conventional Commit prefixes: `feat:`, `fix:`, `docs:`,
`ci:`, and `chore(release):`. Write concise imperative subjects, e.g.
`fix: handle empty media title`. Keep commits scoped to one change. Pull
requests should explain the behavior change, link the relevant issue when one
exists, state validation performed, and include configuration or deployment
notes. Include screenshots only when a user-visible output or documentation
rendering change benefits from one.
