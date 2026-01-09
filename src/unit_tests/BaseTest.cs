using Microsoft.Extensions.DependencyInjection;

namespace NovaTune.UnitTests;

public abstract class BaseTest
{
    private static readonly UnitTestServiceProviderFactory UnitTestServiceProviderFactory;

    static BaseTest()
    {
        UnitTestServiceProviderFactory = new UnitTestServiceProviderFactory(GlobalTestServicesInitializer.Initialize);
    }

    protected readonly IServiceProvider ServiceProvider;

    protected BaseTest()
    {
        ServiceProvider = BuildServiceProvider(StubServices);
    }

    protected virtual void StubServices(IServiceCollection services)
    {
    }

    private static IServiceProvider BuildServiceProvider(Action<IServiceCollection> configureServices)
    {
        return UnitTestServiceProviderFactory.BuildServiceProvider(configureServices);
    }
}
