# AGENTS.md

## What this is

- .NET 10 global tool (`milkyware-wam-cli`) that acquires an access token through the Windows WAM broker and prints it as JSON.
- Target framework is `net10.0` (SDK 10.0.x, unpinned; there is no `global.json`).
- One project: `src/MilkyWare.Wam.Cli`; solution file is `MilkyWare.Wam.Cli.slnx`. There is no test project.
- Runtime is effectively Windows-only: the WAM broker and the `user32.dll` P/Invoke in `GetAccessTokenCommand` require Windows, even though CI builds on Ubuntu.

## Commands

```powershell
dotnet build MilkyWare.Wam.Cli.slnx
dotnet run --project src/MilkyWare.Wam.Cli --no-launch-profile -- --help
dotnet run --project src/MilkyWare.Wam.Cli --no-launch-profile -- -c <client-id> [-t <tenant-id>] [-s <scope> ...] [--clear-cache] [--debug]
dotnet pack src/MilkyWare.Wam.Cli
```

- No tests exist; CI (`.github/workflows/ci.yml`) calls the shared `build-dotnet` action with `skip-tests: true`. Do not add a `dotnet test` step expecting coverage.
- `dotnet run` automatically uses the gitignored `src/MilkyWare.Wam.Cli/Properties/launchSettings.json` when it exists, which may contain local client IDs and `--clear-cache`. Pass `--no-launch-profile` for a clean run.
- A live run needs a real Entra app registration; CI cannot verify token acquisition.

## Architecture

- `Program.cs` wires Spectre.Console.Cli to Microsoft DI through the custom `TypeRegistrar`/`TypeResolver`; register services in `Program.cs` and inject command dependencies through the constructor.
- The single command is `Commands/GetAccessTokenCommand.cs`; its options live in `GetAccessTokenSettings.cs`. Settings derive from `LoggingSettings` so the interceptor can see them.
- Logging is Serilog, default level Warning. `--debug` changes the static `LoggingInterceptor.LogLevel` to Verbose; a settings class that does not derive from `LoggingSettings` will not get that behavior.
- Token cache is `%LOCALAPPDATA%\.wam-cli\msal.contoso.cache`; `--clear-cache` removes all cached accounts before acquisition.
- The scope default in code is `https://graph.microsoft.com/.default` (repeated `-s` flags append). The README help output mentioning `User.Read` is stale; trust `GetAccessTokenSettings`.

## Conventions and releases

- Match `.editorconfig`: private fields `_camelCase`, private statics `s_camelCase`, `var` preferred, Allman braces, block-scoped namespaces.
- Use Conventional Commits. release-please on `main` derives versions and `CHANGELOG.md`; custom types are defined in `release-please-config.json` (including `breaking`).
- Do not hand-edit `CHANGELOG.md` or `PackageVersion` in `MilkyWare.Wam.Cli.csproj`; release-please owns both (the version sits between `x-release-please-start-version` and `end` markers).
- `Properties/launchSettings.json` is intentionally gitignored because it holds local app IDs; never commit it.
