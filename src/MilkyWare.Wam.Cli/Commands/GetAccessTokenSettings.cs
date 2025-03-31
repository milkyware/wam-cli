using Spectre.Console;
using Spectre.Console.Cli;

namespace ConsoleWamBroker.Commands
{
    public class GetAccessTokenSettings : LoggingSettings
    {
        [CommandOption("--clear-cache")]
        public bool ClearCache { get; set; }

        [CommandOption("-c|--client-id")]
        public string? ClientId { get; set; }

        [CommandOption("-s|--scope <VALUES>")]
        public string[] Scopes { get; set; } = ["User.Read"];

        [CommandOption("-t|--tenant-id")]
        public string? TenantId { get; set; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ClientId))
            {
                return ValidationResult.Error("ClientId is required.");
            }

            return ValidationResult.Success();
        }
    }
}
