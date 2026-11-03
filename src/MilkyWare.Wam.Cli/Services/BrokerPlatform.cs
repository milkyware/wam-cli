using System.Runtime.InteropServices;
using Microsoft.Identity.Client;
using MilkyWare.Wam.Cli.Enums;

namespace MilkyWare.Wam.Cli.Services
{
    public sealed class BrokerPlatform(IOperatingSystem operatingSystem) : IBrokerPlatform
    {
        private const string MacOsUnsignedAppRedirectUri = "msauth.com.msauth.unsignedapp://auth";

        private enum GetAncestorFlags
        {
            GetParent = 1,
            GetRoot = 2,
            GetRootOwner = 3
        }

        public BrokerOptions.OperatingSystems BrokerOperatingSystem => operatingSystem.Current switch
        {
            Platform.Windows => BrokerOptions.OperatingSystems.Windows,
            Platform.Linux => BrokerOptions.OperatingSystems.Linux,
            Platform.MacOS => BrokerOptions.OperatingSystems.OSX,
            _ => throw new PlatformNotSupportedException("The broker is not supported on the current operating system."),
        };

        // The macOS broker requires the unsigned executable redirect URI for console apps.
        public string? RedirectUri => BrokerOperatingSystem == BrokerOptions.OperatingSystems.OSX
            ? MacOsUnsignedAppRedirectUri
            : null;

        // macOS broker flows do not support a parent window handle, so only Windows and Linux set one.
        public Func<IntPtr>? ParentWindowFunc => BrokerOperatingSystem switch
        {
            BrokerOptions.OperatingSystems.Windows => GetConsoleOrTerminalWindow,
            BrokerOptions.OperatingSystems.Linux => GetLinuxConsoleWindow,
            _ => null,
        };

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("libX11")]
        private static extern IntPtr XOpenDisplay(IntPtr display);

        [DllImport("libX11")]
        private static extern IntPtr XDefaultRootWindow(IntPtr display);

        private static IntPtr GetConsoleOrTerminalWindow()
        {
            IntPtr consoleHandle = GetConsoleWindow();
            IntPtr handle = GetAncestor(consoleHandle, GetAncestorFlags.GetRootOwner);

            return handle;
        }

        private static IntPtr GetLinuxConsoleWindow()
        {
            try
            {
                IntPtr display = XOpenDisplay(IntPtr.Zero);

                return display == IntPtr.Zero ? IntPtr.Zero : XDefaultRootWindow(display);
            }
            catch (DllNotFoundException)
            {
                return IntPtr.Zero;
            }
            catch (EntryPointNotFoundException)
            {
                return IntPtr.Zero;
            }
        }
    }
}
