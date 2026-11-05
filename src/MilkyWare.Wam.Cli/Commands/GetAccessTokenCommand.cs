using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkyWare.Wam.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MilkyWare.Wam.Cli.Commands
{
    public class GetAccessTokenCommand(ITokenService tokenService, ILogger<GetAccessTokenCommand> logger, IAnsiConsole ansiConsole) : AsyncCommand<GetAccessTokenSettings>
    {
        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };

        private readonly IAnsiConsole _ansiConsole = ansiConsole;
        private readonly ILogger<GetAccessTokenCommand> _logger = logger;
        private readonly ITokenService _tokenService = tokenService;

        public override async Task<int> ExecuteAsync(CommandContext context, GetAccessTokenSettings settings, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settings.ClientId);

            var accessToken = await _tokenService.AcquireAccessTokenAsync(settings, cancellationToken);

            if (accessToken is null)
            {
                return 0;
            }

            var json = JsonSerializer.Serialize(new { accessToken }, s_jsonSerializerOptions);

            _logger.LogDebug("Returning access token");
            _ansiConsole.Write(json);
            return 0;
        }
    }
}
