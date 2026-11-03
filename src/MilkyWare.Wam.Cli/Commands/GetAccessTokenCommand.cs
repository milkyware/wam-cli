using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Identity.Client.Utils;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MilkyWare.Wam.Cli.Commands
{
    public class GetAccessTokenCommand(ILogger<GetAccessTokenCommand> logger, IAnsiConsole ansiConsole) : AsyncCommand<GetAccessTokenSettings>
    {
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };

        private readonly IAnsiConsole _ansiConsole = ansiConsole;
        private readonly ILogger<GetAccessTokenCommand> _logger = logger;

        private enum GetAncestorFlags
        {
            GetParent = 1,
            GetRoot = 2,
            GetRootOwner = 3
        }

        public override async Task<int> ExecuteAsync(CommandContext context, GetAccessTokenSettings settings, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.ClientId);

            _logger.LogDebug("Building client app");
            var brokerOperatingSystem = GetBrokerOperatingSystem();
            _logger.LogDebug("Using {BrokerOperatingSystem} broker", brokerOperatingSystem);
            var builder = PublicClientApplicationBuilder.Create(settings.ClientId)
                .WithBroker(new BrokerOptions(brokerOperatingSystem));

            if (brokerOperatingSystem == BrokerOptions.OperatingSystems.OSX)
            {
                // The macOS broker requires the unsigned executable redirect URI for console apps.
                builder.WithRedirectUri("msauth.com.msauth.unsignedapp://auth");
            }
            else
            {
                builder.WithDefaultRedirectUri();
            }

            // macOS broker flows do not support a parent window handle, so only Windows and Linux set one.
            if (brokerOperatingSystem == BrokerOptions.OperatingSystems.Windows)
            {
                builder.WithParentActivityOrWindow(GetConsoleOrTerminalWindow);
            }
            else if (brokerOperatingSystem == BrokerOptions.OperatingSystems.Linux)
            {
                builder.WithParentActivityOrWindow(GetLinuxConsoleWindow);
            }

            if (!string.IsNullOrWhiteSpace(settings.TenantId))
            {
                _logger.LogDebug("Configuring tenant Id");
                builder.WithTenantId(settings.TenantId);
            }

            var app = builder.Build();

            _logger.LogDebug("Configuring cache");
            var filePath = Path.Combine(MsalCacheHelper.UserRootDirectory, ".wam-cli", "msal.contoso.cache");
            var storageProperties = new StorageCreationPropertiesBuilder(Path.GetFileName(filePath),
                Path.GetDirectoryName(filePath))
                .WithLinuxKeyring(
                    "com.milkyware.wam-cli",
                    "default",
                    "MSALCache",
                    new KeyValuePair<string, string>("MsalClientID", "Microsoft.Developer.IdentityService"),
                    new KeyValuePair<string, string>("MsalClientVersion", "1.0.0.0"))
                .WithMacKeyChain("com.milkyware.wam-cli", "MSALCache")
                .Build();

            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(app.UserTokenCache);

            var accounts = await app.GetAccountsAsync();

            if (settings.ClearCache)
            {
                _logger.LogDebug("Clearing cache");
                while (accounts.Any())
                {
                    await app.RemoveAsync(accounts.First());
                    accounts = await app.GetAccountsAsync();
                }
            }

            var account = accounts.FirstOrDefault() ?? PublicClientApplication.OperatingSystemAccount;

            AuthenticationResult authResult;
            try
            {
                authResult = await AcquireTokenAsync(app, account, settings.Scopes);
            }
            catch (MsalException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
            {
                _logger.LogWarning("Authentication was cancelled");
                return 0;
            }

            var json = JsonSerializer.Serialize(new { authResult.AccessToken }, s_jsonSerializerOptions);

            _logger.LogDebug("Returning access token");
            _ansiConsole.Write(json);
            return 0;
        }

        #region Private Helpers

        private static BrokerOptions.OperatingSystems GetBrokerOperatingSystem() => true switch
        {
            _ when OperatingSystem.IsWindows() => BrokerOptions.OperatingSystems.Windows,
            _ when OperatingSystem.IsLinux() => BrokerOptions.OperatingSystems.Linux,
            _ when OperatingSystem.IsMacOS() => BrokerOptions.OperatingSystems.OSX,
            _ => throw new PlatformNotSupportedException("The broker is not supported on the current operating system."),
        };

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);

        private static IntPtr GetConsoleOrTerminalWindow()
        {
            IntPtr consoleHandle = GetConsoleWindow();
            IntPtr handle = GetAncestor(consoleHandle, GetAncestorFlags.GetRootOwner);

            return handle;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("libX11")]
        private static extern IntPtr XOpenDisplay(IntPtr display);

        [DllImport("libX11")]
        private static extern IntPtr XDefaultRootWindow(IntPtr display);

        private static IntPtr GetLinuxConsoleWindow()
        {
            try
            {
                IntPtr display = XOpenDisplay(IntPtr.Zero);

                return display == IntPtr.Zero ? IntPtr.Zero : XDefaultRootWindow(display);
            }
            catch (DllNotFoundException)
            {
                return IntPtr.Zero;
            }
            catch (EntryPointNotFoundException)
            {
                return IntPtr.Zero;
            }
        }

        private async Task<AuthenticationResult> AcquireTokenAsync(IPublicClientApplication app, IAccount account, IEnumerable<string> scopes)
        {
            AuthenticationResult authResult;
            try
            {
                _logger.LogDebug("Trying to get token silently");
                authResult = await app.AcquireTokenSilent(scopes, account)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                authResult = await AcquireTokenInteractiveAsync(app, scopes);
            }

            return authResult;
        }

        private async Task<AuthenticationResult> AcquireTokenInteractiveAsync(IPublicClientApplication app, IEnumerable<string> scopes)
        {
            _logger.LogDebug("Getting token interactively");

            var mainThreadScheduler = MacMainThreadScheduler.Instance();
            if (!OperatingSystem.IsMacOS() || !mainThreadScheduler.IsRunning() || mainThreadScheduler.IsCurrentlyOnMainThread())
            {
                return await app.AcquireTokenInteractive(scopes)
                    .ExecuteAsync();
            }

            AuthenticationResult? authResult = null;
            await mainThreadScheduler.RunOnMainThreadAsync(async () =>
            {
                authResult = await app.AcquireTokenInteractive(scopes)
                    .ExecuteAsync()
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);

            return authResult ?? throw new InvalidOperationException("Interactive authentication did not return a result.");
        }

        #endregion Private Helpers
    }
}
