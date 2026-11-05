# AGENTS.md

## What this is

- .NET 10 global tool (`milkyware-wam-cli`) that acquires an access token through the platform authentication broker and prints it as JSON.
- Target framework is `net10.0` (SDK 10.0.x, unpinned). `global.json` only selects the MTP test runner; it pins no SDK version.
- Two projects: `src/MilkyWare.Wam.Cli` (the tool) and `tests/MilkyWare.Wam.CliTests` (xUnit v3 on MTP); solution file is `MilkyWare.Wam.Cli.slnx`.
- Runs on Windows and Linux. `BrokerPlatform` maps `Enums.Platform` to `BrokerOptions.OperatingSystems` and throws `PlatformNotSupportedException` for any other OS. The `user32.dll`/`kernel32.dll` P/Invokes are Windows-only and the `libX11` P/Invokes are Linux-only, so neither is called on the other platform.

## Commands

```powershell
dotnet build MilkyWare.Wam.Cli.slnx
dotnet test MilkyWare.Wam.Cli.slnx
dotnet test --project tests/MilkyWare.Wam.CliTests --coverlet
dotnet run --project src/MilkyWare.Wam.Cli --no-launch-profile -- --help
dotnet run --project src/MilkyWare.Wam.Cli --no-launch-profile -- -c <client-id> [-t <tenant-id>] [-s <scope> ...] [--clear-cache] [--debug]
dotnet pack src/MilkyWare.Wam.Cli
```

- CI (`.github/workflows/ci.yml`) calls the local composite action `.github/actions/build-dotnet` (restore, build, then `dotnet test` without coverage). Coverage (`--coverlet`) is a local option only and writes to `TestResults/`; do not add it to CI without deciding where the artifacts go.
- `dotnet run` automatically uses the gitignored `src/MilkyWare.Wam.Cli/Properties/launchSettings.json` when it exists, which may contain local client IDs and `--clear-cache`. Pass `--no-launch-profile` for a clean run.
- A live run needs a real Entra app registration; CI cannot verify token acquisition.

## Architecture

- `Program.cs` wires Spectre.Console.Cli to Microsoft DI through the custom `TypeRegistrar`/`TypeResolver`; register services in `Program.cs` and inject command dependencies through the constructor.
- `Services/` holds the MSAL logic: `IBrokerPlatform` maps the current `Enums.Platform` to `BrokerOptions.OperatingSystems` and supplies the parent-window P/Invokes, `ITokenService`/`MsalTokenService` builds the client app, configures the cache, and performs silent/interactive acquisition (returning the access token, or `null` when the user cancels), and `IOperatingSystem` (`HostOs`) reports the host `Platform` as the test seam over the static `OperatingSystem` checks. Register them through `AddWamCliServices` in `ServiceCollectionExtensions` (namespace `MilkyWare.Extensions.DependencyInjection`), which also registers `LoggingInterceptor`.
- The single command is `Commands/GetAccessTokenCommand.cs`, which validates the client ID, delegates to `ITokenService`, and writes the JSON; its options live in `GetAccessTokenSettings.cs`. Settings derive from `LoggingSettings` so the interceptor can see them.
- Logging is Serilog, default level Warning. `--debug` changes the static `LoggingInterceptor.LogLevel` to Verbose; a settings class that does not derive from `LoggingSettings` will not get that behavior.
- Token cache is `MsalCacheHelper.UserRootDirectory/.wam-cli/msal.contoso.cache` (on Windows `%LOCALAPPDATA%\.wam-cli\msal.contoso.cache`); `--clear-cache` removes all cached accounts before acquisition. The storage properties must chain `.WithLinuxKeyring(...)`; without it, `MsalCacheHelper.CreateAsync` throws `ArgumentNullException (attributeKey1)` from `LinuxKeyringAccessor`.
- The Linux broker's native store (`Msai::SecureStorage` in `libmsalruntime.so`) hardcodes the Secret Service collection `login` and never creates it. WSL has no PAM login to provision it, so without a `login` keyring every broker write fails (`WriteNoLock ... /collection/login`) and nothing persists — runs keep prompting. Our own cache uses the `default` collection and is unaffected.
- Silent acquisition is only attempted when `GetAccountsAsync` returns an account. With no cached account the Linux broker blocks inside its background request instead of throwing `MsalUiRequiredException`, so `MsalTokenService` goes straight to interactive.
- MSAL itself logs through the `LogCallback` overload of `WithLogging` with PII logging off. Messages matching `s_benignSignatures` in `MsalTokenService` are demoted to debug because they describe expected control flow; revisit that list on MSAL upgrades since it matches on message text.
- The scope default in code is `https://graph.microsoft.com/.default` (repeated `-s` flags append). The README help output mentioning `User.Read` is stale; trust `GetAccessTokenSettings`.

## Tests

- `tests/MilkyWare.Wam.CliTests` is xUnit v3 (`xunit.v3.mtp-v2`) on Microsoft Testing Platform; `global.json`'s `test.runner` selects MTP mode so plain `dotnet test` works on the .NET 10 SDK. `UseMicrosoftTestingPlatformRunner` and `OutputType=Exe` are required for the native runner.
- Test classes mirror the component folder (`Services/`, `DependencyInjection/`) and namespaces add `.Tests`. Use NSubstitute for mocks and FluentAssertions for assertions, build mocks plus the SUT in the constructor as private readonly fields, expose the ambient token through a `CancellationToken => _contextAccessor.Current.CancellationToken` getter (`ITestContextAccessor` is constructor-injected), and write every test as Arrange/Act/Assert.
- `MsalTokenService.LogMsal` is `internal` with `InternalsVisibleTo("MilkyWare.Wam.CliTests")` so the log demotion matrix is testable; the rest of `AcquireAccessTokenAsync` is covered by live broker runs instead of mocks.
- Coverage uses `coverlet.MTP` via `dotnet test --coverlet` (add `--coverlet-output-format cobertura` for reports in `TestResults/`); it is not part of CI.

## Conventions and releases

- Match `.editorconfig`: private fields `_camelCase`, private static fields `s_camelCase`, `var` preferred, Allman braces, block-scoped namespaces.
- Use Conventional Commits. release-please on `main` derives versions and `CHANGELOG.md`; custom types are defined in `release-please-config.json` (including `breaking`).
- Releases publish to nuget.org from `.github/workflows/release-cli.yml` through trusted publishing: `nuget/login` exchanges a GitHub OIDC token for a short-lived key, and the `NUGET_USERNAME` repository secret holds the nuget.org profile name. The nuget.org policy names `release-cli.yml` (the workflow containing the login step), not `release-please.yml`.
- The repository is public: do not reference private `milkyware/actions` or the `github>milkyware/actions` Renovate preset. Reusable workflow logic belongs in `.github/actions`.
- Do not hand-edit `CHANGELOG.md` or `PackageVersion` in `MilkyWare.Wam.Cli.csproj`; release-please owns both (the version sits between `x-release-please-start-version` and `end` markers).
- `Properties/launchSettings.json` is intentionally gitignored because it holds local app IDs; never commit it.
