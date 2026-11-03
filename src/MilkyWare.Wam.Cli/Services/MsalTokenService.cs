using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Identity.Client.Utils;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.IdentityModel.LoggingExtensions;
using MilkyWare.Wam.Cli.Commands;

namespace MilkyWare.Wam.Cli.Services
{
    public sealed class MsalTokenService(
        ILogger<MsalTokenService> logger,
        IBrokerPlatform brokerPlatform,
        IIdentityLogger identityLogger) : ITokenService
    {
        public async Task<string?> AcquireAccessTokenAsync(GetAccessTokenSettings settings, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.ClientId);

            logger.LogDebug("Building client app");
            var builder = PublicClientApplicationBuilder.Create(settings.ClientId)
                .WithBroker(new BrokerOptions(brokerPlatform.BrokerOperatingSystem))
                .WithLogging(identityLogger);

            if (brokerPlatform.RedirectUri is { } redirectUri)
            {
                builder.WithRedirectUri(redirectUri);
            }
            else
            {
                builder.WithDefaultRedirectUri();
            }

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

            if (brokerPlatform.BrokerOperatingSystem != BrokerOptions.OperatingSystems.OSX)
            {
                return await app.AcquireTokenInteractive(scopes)
                    .ExecuteAsync(cancellationToken);
            }

            // The macOS broker requires interactive calls on the main thread.
            var mainThreadScheduler = MacMainThreadScheduler.Instance();
            if (!mainThreadScheduler.IsRunning() || mainThreadScheduler.IsCurrentlyOnMainThread())
            {
                return await app.AcquireTokenInteractive(scopes)
                    .ExecuteAsync(cancellationToken);
            }

            AuthenticationResult? authResult = null;
            await mainThreadScheduler.RunOnMainThreadAsync(async () =>
            {
                authResult = await app.AcquireTokenInteractive(scopes)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);

            return authResult ?? throw new InvalidOperationException("Interactive authentication did not return a result.");
        }
    }
}
