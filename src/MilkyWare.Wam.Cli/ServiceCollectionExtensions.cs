using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Abstractions;
using Microsoft.IdentityModel.LoggingExtensions;
using MilkyWare.Wam.Cli.Interceptors;
using MilkyWare.Wam.Cli.Services;
using Spectre.Console.Cli;

namespace MilkyWare.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWamCliServices(this IServiceCollection services)
        {
            services.AddSingleton<IIdentityLogger>(sp =>
                new IdentityLoggerAdapter(sp.GetRequiredService<ILogger<IdentityLoggerAdapter>>()));
            services.AddSingleton<IOperatingSystem, HostOs>();
            services.AddSingleton<IBrokerPlatform, BrokerPlatform>();
            services.AddTransient<ITokenService, MsalTokenService>();
            services.AddTransient<ICommandInterceptor, LoggingInterceptor>();

            return services;
        }
    }
}
