using Microsoft.Extensions.DependencyInjection;
using MilkyWare.Wam.Cli.Interceptors;
using MilkyWare.Wam.Cli.Services;
using Spectre.Console.Cli;

namespace MilkyWare.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWamCliServices(this IServiceCollection services)
        {
            services.AddSingleton<IOperatingSystem, HostOs>();
            services.AddSingleton<IBrokerPlatform, BrokerPlatform>();
            services.AddTransient<ITokenService, MsalTokenService>();
            services.AddTransient<ICommandInterceptor, LoggingInterceptor>();

            return services;
        }
    }
}
