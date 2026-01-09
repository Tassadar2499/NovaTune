using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NovaTune.UnitTests.Fakes;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Identity;
using NovaTuneApp.ApiService.Models.Identity;
using NovaTuneApp.ApiService.Services;

namespace NovaTune.UnitTests;

public static class GlobalTestServicesInitializer
{
    public static void Initialize(IServiceCollection serviceCollection)
    {
        // Token Service
        serviceCollection.AddSingleton<ITokenService, TokenServiceFake>();
        serviceCollection.AddSingleton(s => (TokenServiceFake)s.GetRequiredService<ITokenService>());

        // User Store (implements both IUserStore and IUserEmailStore)
        serviceCollection.AddSingleton<UserStoreFake>();
        serviceCollection.AddSingleton<IUserStore<ApplicationUser>>(s => s.GetRequiredService<UserStoreFake>());
        serviceCollection.AddSingleton<IUserEmailStore<ApplicationUser>>(s => s.GetRequiredService<UserStoreFake>());

        // Password Hasher
        serviceCollection.AddSingleton<IPasswordHasher<ApplicationUser>, PasswordHasherFake>();
        serviceCollection.AddSingleton(s => (PasswordHasherFake)s.GetRequiredService<IPasswordHasher<ApplicationUser>>());

        // Refresh Token Repository
        serviceCollection.AddSingleton<IRefreshTokenRepository, RefreshTokenRepositoryFake>();
        serviceCollection.AddSingleton(s => (RefreshTokenRepositoryFake)s.GetRequiredService<IRefreshTokenRepository>());

        // Session Settings
        serviceCollection.AddSingleton<IOptions<SessionSettings>>(Options.Create(new SessionSettings
        {
            MaxConcurrentSessions = 5
        }));

        // Logger
        serviceCollection.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Auth Service
        serviceCollection.AddSingleton<AuthService>();
        serviceCollection.AddSingleton<IAuthService>(s => s.GetRequiredService<AuthService>());
    }
}