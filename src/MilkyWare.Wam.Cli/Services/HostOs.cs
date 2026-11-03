using MilkyWare.Wam.Cli.Enums;

namespace MilkyWare.Wam.Cli.Services
{
    public sealed class HostOs : IOperatingSystem
    {
        public Platform Current => true switch
        {
            _ when OperatingSystem.IsWindows() => Platform.Windows,
            _ when OperatingSystem.IsLinux() => Platform.Linux,
            _ when OperatingSystem.IsMacOS() => Platform.MacOS,
            _ => Platform.Unknown,
        };
    }
}
