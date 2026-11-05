using MilkyWare.Wam.Cli.Commands;

namespace MilkyWare.Wam.Cli.Services
{
    public interface ITokenService
    {
        /// <summary>
        /// Acquires an access token for the supplied settings.
        /// </summary>
        /// <returns>The access token, or <see langword="null"/> when authentication was cancelled.</returns>
        Task<string?> AcquireAccessTokenAsync(GetAccessTokenSettings settings, CancellationToken cancellationToken);
    }
}
