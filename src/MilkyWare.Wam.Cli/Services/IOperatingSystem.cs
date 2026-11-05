using MilkyWare.Wam.Cli.Enums;

namespace MilkyWare.Wam.Cli.Services
{
    public interface IOperatingSystem
    {
        /// <summary>
        /// Gets the operating system the current process is running on.
        /// </summary>
        Platform Current { get; }
    }
}
