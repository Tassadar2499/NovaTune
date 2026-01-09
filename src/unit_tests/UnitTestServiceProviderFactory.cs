using Microsoft.Extensions.DependencyInjection;

namespace NovaTune.UnitTests;

public class UnitTestServiceProviderFactory
{
    private readonly Action<IServiceCollection> _globalInitAction;

    public UnitTestServiceProviderFactory(Action<IServiceCollection> globalInitAction)
    {
        _globalInitAction = globalInitAction;
    }

    public IServiceProvider BuildServiceProvider(Action<IServiceCollection> configureServices)
    {
        var serviceCollection = new ServiceCollection();

        _globalInitAction(serviceCollection);
        configureServices(serviceCollection);

        var options = new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        };

        return serviceCollection.BuildServiceProvider(options);
    }
}
