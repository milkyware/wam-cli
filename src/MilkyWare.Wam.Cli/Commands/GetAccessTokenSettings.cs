using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MilkyWare.Wam.Cli.Commands
{
    public class GetAccessTokenSettings : LoggingSettings
    {
        [CommandOption("--clear-cache")]
        [Description("Clear the token cache before acquiring a new token")]
        public bool ClearCache { get; set; }

        [CommandOption("-c|--client-id")]
        [Description("Required. The client ID of the app reg")]
        public string? ClientId { get; set; }

        [CommandOption("-s|--scope <VALUES>")]
        [Description("Optional. The scopes required for the token. Defaults to .default scope")]
        public string[] Scopes { get; set; } = ["https://graph.microsoft.com/.default"];

        [CommandOption("-t|--tenant-id")]
        [Description("Optional. The tenant ID of the single tenancy App Reg")]
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
