using ConsoleWamBroker;
using ConsoleWamBroker.Commands;
using ConsoleWamBroker.Interceptors;
using Microsoft.Extensions.DependencyInjection;
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
#if DEBUG
    configure.PropagateExceptions();
    configure.ValidateExamples();
#endif
});

app.Run(args);
