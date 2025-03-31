using System.ComponentModel;
using Spectre.Console.Cli;

namespace ConsoleWamBroker.Commands
{
    public class LoggingSettings : CommandSettings
    {
        [CommandOption("--debug")]
        [Description("Increase logging verbosity to show all debug logs.")]
        [DefaultValue(false)]
        public bool Debug { get; set; }
    }
}
