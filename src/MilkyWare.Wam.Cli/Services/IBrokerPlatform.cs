using Microsoft.Identity.Client;

namespace MilkyWare.Wam.Cli.Services
{
    public interface IBrokerPlatform
    {
        /// <summary>
        /// Gets the broker operating system used to configure the MSAL broker.
        /// </summary>
        BrokerOptions.OperatingSystems BrokerOperatingSystem { get; }

        /// <summary>
        /// Gets the redirect URI required by the platform broker, or <see langword="null"/> to use the MSAL default.
        /// </summary>
        string? RedirectUri { get; }

        /// <summary>
        /// Gets the parent window handle provider for the interactive prompt, or <see langword="null"/> when unsupported.
        /// </summary>
        Func<IntPtr>? ParentWindowFunc { get; }
    }
}
