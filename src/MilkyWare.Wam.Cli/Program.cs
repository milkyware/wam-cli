using Microsoft.Extensions.DependencyInjection;
using MilkyWare.Wam.Cli;
using MilkyWare.Wam.Cli.Commands;
using MilkyWare.Wam.Cli.Interceptors;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddSerilog(new LoggerConfiguration()
        .MinimumLevel.ControlledBy(LoggingInterceptor.LogLevel)
        .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
        .CreateLogger());
});
services.AddTransient<ICommandInterceptor, LoggingInterceptor>();
var registrar = new TypeRegistrar(services);
var app = new CommandApp<GetAccessTokenCommand>(registrar);
app.Configure(configure =>
{
    configure.SetApplicationName("milkyware-wam-cli");

    configure.AddExample("-c", Guid.Empty.ToString());
    configure.AddExample("-c", Guid.Empty.ToString(), "-t", Guid.Empty.ToString());
    configure.AddExample("-c", Guid.Empty.ToString(), "-s", "User.Read", "-s", "Mail.Read");
    configure.AddExample("-c", Guid.Empty.ToString(), "--clear-cache");

#if DEBUG
    configure.PropagateExceptions();
    configure.ValidateExamples();
#endif
});

app.Run(args);
