using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
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

        protected override async Task<int> ExecuteAsync(CommandContext context, GetAccessTokenSettings settings, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.ClientId);

            _logger.LogDebug("Building client app");
            var builder = PublicClientApplicationBuilder.Create(settings.ClientId)
                .WithDefaultRedirectUri()
                .WithParentActivityOrWindow(GetConsoleOrTerminalWindow)
                .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows));

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

        private async Task<AuthenticationResult> AcquireTokenAsync(IPublicClientApplication app, IAccount account, IEnumerable<string> scopes)
        {
            AuthenticationResult authResult;
            try
            {
                _logger.LogDebug("Trying to get token silently");
                authResult = await app.AcquireTokenSilent(scopes, account)
                    .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                _logger.LogDebug("Getting token interactively");
                authResult = await app.AcquireTokenInteractive(scopes)
                    .ExecuteAsync();
            }

            return authResult;
        }

        #endregion Private Helpers
    }
}
