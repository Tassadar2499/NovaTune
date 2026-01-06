using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NovaTuneApp.ApiService.Infrastructure.Caching;
using NovaTuneApp.ApiService.Infrastructure.Configuration;
using NovaTuneApp.ApiService.Infrastructure.Identity;
using NovaTuneApp.ApiService.Infrastructure.Messaging;
using NovaTuneApp.ApiService.Infrastructure.RateLimiting;
using NovaTuneApp.ApiService.Models.Identity;
using NovaTuneApp.ApiService.Services;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Serilog;

namespace NovaTuneApp.Tests;

/// <summary>
/// Custom WebApplicationFactory for auth integration tests.
/// Mocks external dependencies (RavenDB, Redis, Kafka) for isolated testing.
/// </summary>
public class AuthApiFactory : WebApplicationFactory<Program>
{
    // In-memory storage for test isolation
    private readonly Dictionary<string, ApplicationUser> _users = new();
    private readonly Dictionary<string, RefreshToken> _tokens = new();

    public IUserStore<ApplicationUser> UserStore { get; private set; } = null!;
    public IRefreshTokenRepository RefreshTokenRepository { get; private set; } = null!;

    public AuthApiFactory()
    {
        // Reset Serilog before creating the factory to avoid disposed provider references
        Log.CloseAndFlush();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Configure Serilog without service provider capture for test isolation
        builder.UseSerilog((_, configuration) => configuration
            .MinimumLevel.Warning()
            .WriteTo.Console());

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use a simple logger for tests to avoid Serilog service provider capture issues
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration with high rate limits for most tests
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SIGNING_KEY"] = "test-signing-key-must-be-at-least-32-characters-long-for-auth-tests",
                ["Jwt:Issuer"] = "https://test.novatune.example",
                ["Jwt:Audience"] = "novatune-test-api",
                ["Jwt:AccessTokenExpirationMinutes"] = "15",
                ["Jwt:RefreshTokenExpirationMinutes"] = "60",
                // High rate limits for general tests - rate limit tests use a separate factory
                ["RateLimiting:Auth:LoginPerIp:PermitLimit"] = "1000",
                ["RateLimiting:Auth:LoginPerIp:WindowMinutes"] = "1",
                ["RateLimiting:Auth:LoginPerAccount:PermitLimit"] = "1000",
                ["RateLimiting:Auth:LoginPerAccount:WindowMinutes"] = "1",
                ["RateLimiting:Auth:RegisterPerIp:PermitLimit"] = "1000",
                ["RateLimiting:Auth:RegisterPerIp:WindowMinutes"] = "1",
                ["RateLimiting:Auth:RefreshPerIp:PermitLimit"] = "1000",
                ["RateLimiting:Auth:RefreshPerIp:WindowMinutes"] = "1",
                ["RavenDb:Url"] = "http://localhost:8080",
                ["RavenDb:Database"] = "Test",
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:TopicPrefix"] = "test",
                ["NovaTune:TopicPrefix"] = "test",
                ["NovaTune:PresignedUrl:TtlSeconds"] = "300",
                ["ConnectionStrings:cache"] = "localhost:6379"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove real infrastructure services
            services.RemoveAll<IDocumentStore>();
            services.RemoveAll<IAsyncDocumentSession>();
            services.RemoveAll<IUserStore<ApplicationUser>>();
            services.RemoveAll<IRefreshTokenRepository>();
            services.RemoveAll<ICacheService>();
            services.RemoveAll<IMessageProducerService>();
            services.RemoveAll<IHostedService>();

            // Configure high rate limits for testing (override default rate limiter)
            services.Configure<RateLimitSettings>(opts =>
            {
                opts.Auth = new AuthRateLimits
                {
                    LoginPerIp = new RateLimitPolicy(10000, 1),
                    LoginPerAccount = new RateLimitPolicy(10000, 1),
                    RegisterPerIp = new RateLimitPolicy(10000, 1),
                    RefreshPerIp = new RateLimitPolicy(10000, 1)
                };
            });

            // Replace LoginRateLimiterPolicy - will be resolved via IOptions<RateLimitSettings>
            services.RemoveAll<LoginRateLimiterPolicy>();
            services.AddSingleton<LoginRateLimiterPolicy>();

            // Create in-memory stores
            UserStore = CreateInMemoryUserStore();
            RefreshTokenRepository = CreateInMemoryRefreshTokenRepository();

            // Register mock services
            services.AddSingleton(UserStore);
            services.AddSingleton(RefreshTokenRepository);
            services.AddSingleton(Substitute.For<ICacheService>());
            services.AddSingleton(Substitute.For<IMessageProducerService>());
        });
    }

    private IUserStore<ApplicationUser> CreateInMemoryUserStore()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>, IUserEmailStore<ApplicationUser>>();
        var emailStore = (IUserEmailStore<ApplicationUser>)store;

        // CreateAsync - store user in memory
        store.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<ApplicationUser>();
                user.UserId = Ulid.NewUlid().ToString();
                _users[user.NormalizedEmail] = user;
                return Task.FromResult(IdentityResult.Success);
            });

        // FindByEmailAsync - lookup by normalized email
        emailStore.FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var normalizedEmail = callInfo.Arg<string>();
                _users.TryGetValue(normalizedEmail, out var user);
                return Task.FromResult(user);
            });

        // FindByIdAsync - lookup by user ID
        store.FindByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var userId = callInfo.Arg<string>();
                var user = _users.Values.FirstOrDefault(u => u.UserId == userId);
                return Task.FromResult(user);
            });

        // UpdateAsync - update user in memory
        store.UpdateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<ApplicationUser>();
                _users[user.NormalizedEmail] = user;
                return Task.FromResult(IdentityResult.Success);
            });

        return store;
    }

    private IRefreshTokenRepository CreateInMemoryRefreshTokenRepository()
    {
        var repo = Substitute.For<IRefreshTokenRepository>();

        // CreateAsync - store token
        repo.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = new RefreshToken
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = callInfo.ArgAt<string>(0),
                    TokenHash = callInfo.ArgAt<string>(1),
                    ExpiresAt = callInfo.ArgAt<DateTime>(2),
                    DeviceIdentifier = callInfo.ArgAt<string?>(3),
                    CreatedAt = DateTime.UtcNow
                };
                _tokens[token.TokenHash] = token;
                return Task.FromResult(token);
            });

        // FindByHashAsync - lookup by hash
        repo.FindByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var hash = callInfo.Arg<string>();
                _tokens.TryGetValue(hash, out var token);
                if (token != null && !token.IsRevoked && token.ExpiresAt > DateTime.UtcNow)
                    return Task.FromResult<RefreshToken?>(token);
                return Task.FromResult<RefreshToken?>(null);
            });

        // RevokeAsync - mark as revoked
        repo.RevokeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var tokenId = callInfo.Arg<string>();
                var token = _tokens.Values.FirstOrDefault(t => t.Id == tokenId);
                if (token != null)
                    token.IsRevoked = true;
                return Task.CompletedTask;
            });

        // RevokeAllForUserAsync
        repo.RevokeAllForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var userId = callInfo.Arg<string>();
                foreach (var token in _tokens.Values.Where(t => t.UserId == userId))
                    token.IsRevoked = true;
                return Task.CompletedTask;
            });

        // GetActiveCountForUserAsync
        repo.GetActiveCountForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var userId = callInfo.Arg<string>();
                var count = _tokens.Values.Count(t =>
                    t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);
                return Task.FromResult(count);
            });

        // RevokeOldestForUserAsync
        repo.RevokeOldestForUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var userId = callInfo.Arg<string>();
                var oldest = _tokens.Values
                    .Where(t => t.UserId == userId && !t.IsRevoked)
                    .OrderBy(t => t.CreatedAt)
                    .FirstOrDefault();
                if (oldest != null)
                    oldest.IsRevoked = true;
                return Task.CompletedTask;
            });

        return repo;
    }

    /// <summary>
    /// Gets a user by email for test verification.
    /// </summary>
    public ApplicationUser? GetUserByEmail(string email)
    {
        _users.TryGetValue(email.ToUpperInvariant(), out var user);
        return user;
    }

    /// <summary>
    /// Gets the count of active tokens for a user.
    /// </summary>
    public int GetActiveTokenCount(string userId)
    {
        return _tokens.Values.Count(t =>
            t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow);
    }

    /// <summary>
    /// Clears all test data.
    /// </summary>
    public void ClearData()
    {
        _users.Clear();
        _tokens.Clear();
    }
}
