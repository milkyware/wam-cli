using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace MilkyWare.Wam.Cli
{
    public sealed class TypeResolver : ITypeResolver, IDisposable
    {
        private readonly IServiceProvider _provider;

        public TypeResolver(IServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public void Dispose()
        {
            if (_provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public object? Resolve(Type? type)
        {
            if (type == null)
            {
                return null;
            }

            return _provider.GetRequiredService(type);
        }
    }
}
