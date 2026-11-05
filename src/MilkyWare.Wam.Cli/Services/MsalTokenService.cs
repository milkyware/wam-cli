using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Identity.Client.Utils;
using MilkyWare.Wam.Cli.Commands;

namespace MilkyWare.Wam.Cli.Services
{
    public sealed class MsalTokenService(
        ILogger<MsalTokenService> logger,
        IBrokerPlatform brokerPlatform) : ITokenService
    {
        /// <summary>
        /// MSAL messages that describe expected control flow rather than a fault. They are logged at
        /// debug level so real errors and warnings stay visible. Revisit on MSAL upgrades, as this
        /// matches on message text.
        /// </summary>
        private static readonly string[] s_benignSignatures =
        [
            "wam_no_account_for_id",
            "AccountNotFound",
            "MsalUiRequiredException",
            "TryNormalizeRealm",
            "MsaDeviceOperationProvider",
            "ParsePipeDelimitedClientData",
            "ReadAccountById",
            "CalculateSuggestedCacheExpiry",
        ];

        public async Task<string?> AcquireAccessTokenAsync(GetAccessTokenSettings settings, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.ClientId);

            logger.LogDebug("Building client app");
            var builder = PublicClientApplicationBuilder.Create(settings.ClientId)
                .WithBroker(new BrokerOptions(brokerPlatform.BrokerOperatingSystem))
                .WithLogging(LogMsal, Microsoft.Identity.Client.LogLevel.Verbose, enablePiiLogging: false)
                .WithDefaultRedirectUri();

            if (brokerPlatform.ParentWindowFunc is { } parentWindowFunc)
            {
                builder.WithParentActivityOrWindow(parentWindowFunc);
            }

            if (!string.IsNullOrWhiteSpace(settings.TenantId))
            {
                logger.LogDebug("Configuring tenant Id");
                builder.WithTenantId(settings.TenantId);
            }

            var app = builder.Build();

            logger.LogDebug("Configuring cache");
            var filePath = Path.Combine(MsalCacheHelper.UserRootDirectory, ".wam-cli", "msal.contoso.cache");
            var storageProperties = new StorageCreationPropertiesBuilder(Path.GetFileName(filePath),
                Path.GetDirectoryName(filePath))
                .WithLinuxKeyring("com.milkyware.wam-cli",
                    MsalCacheHelper.LinuxKeyRingDefaultCollection,
                    "MSALCache",
                    new("MsalClientID", "Microsoft.Developer.IdentityService"),
                    new("MsalClientVersion", "1.0.0.0"))
                .Build();

            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(app.UserTokenCache);

            var accounts = await app.GetAccountsAsync();

            if (settings.ClearCache)
            {
                logger.LogDebug("Clearing cache");
                while (accounts.Any())
                {
                    await app.RemoveAsync(accounts.First());
                    accounts = await app.GetAccountsAsync();
                }
            }

            try
            {
                // The Linux broker blocks instead of throwing MsalUiRequiredException when there is no
                // cached account, so silent acquisition is only attempted when an account was found.
                var account = accounts.FirstOrDefault();
                var authResult = account is null
                    ? await AcquireTokenInteractiveAsync(app, settings.Scopes, cancellationToken)
                    : await AcquireTokenSilentAsync(app, account, settings.Scopes, cancellationToken);

                return authResult.AccessToken;
            }
            catch (MsalException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
            {
                logger.LogWarning("Authentication was cancelled");
                return null;
            }
        }

        internal void LogMsal(Microsoft.Identity.Client.LogLevel level, string message, bool containsPii)
        {
            if (containsPii)
                return;

            var logLevel = level switch
            {
                Microsoft.Identity.Client.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                Microsoft.Identity.Client.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                Microsoft.Identity.Client.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
                _ => Microsoft.Extensions.Logging.LogLevel.Debug,
            };

            if (!s_benignSignatures.Any(signature => message.Contains(signature, StringComparison.OrdinalIgnoreCase)))
            {
                logger.Log(logLevel, "{Message}", message);
                return;
            }

            logger.LogDebug("{Message}", message);
        }

        private async Task<AuthenticationResult> AcquireTokenSilentAsync(IPublicClientApplication app, IAccount account, IEnumerable<string> scopes, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogDebug("Trying to get token silently");
                return await app.AcquireTokenSilent(scopes, account)
                    .ExecuteAsync(cancellationToken);
            }
            catch (MsalUiRequiredException)
            {
                return await AcquireTokenInteractiveAsync(app, scopes, cancellationToken);
            }
        }

        private async Task<AuthenticationResult> AcquireTokenInteractiveAsync(IPublicClientApplication app, IEnumerable<string> scopes, CancellationToken cancellationToken)
        {
            logger.LogDebug("Getting token interactively");

            return await app.AcquireTokenInteractive(scopes)
                .ExecuteAsync(cancellationToken);
        }
    }
}
