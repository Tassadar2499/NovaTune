using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NovaTune.UnitTests;

public static class ServiceProviderExtensions
{
    public static void StubService<TService, TStub>(this IServiceCollection services, ServiceLifetime stubLifetime = ServiceLifetime.Singleton)
        where TService : class
        where TStub : class
    {
        services.RemoveAll<TService>();
        services.RemoveAll<TStub>();
        services.Add(ServiceDescriptor.Describe(typeof(TStub), typeof(TStub), stubLifetime));
        services.Add(ServiceDescriptor.Describe(typeof(TService), provider => provider.GetRequiredService<TStub>(), stubLifetime));
    }
}
