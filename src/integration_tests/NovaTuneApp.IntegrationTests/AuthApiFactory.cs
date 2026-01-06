using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Serilog;

namespace NovaTuneApp.Tests;

/// <summary>
/// Aspire-based test factory for auth integration tests.
/// Uses real containerized infrastructure (RavenDB, Redis, Kafka).
/// </summary>
public class AuthApiFactory : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private HttpClient _client = null!;
    private IDocumentStore _documentStore = null!;

    public HttpClient Client => _client;

    public AuthApiFactory()
    {
        // Reset Serilog before creating the factory to avoid disposed provider references
        Log.CloseAndFlush();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();
    }

    public async Task InitializeAsync()
    {
        // Configure test-specific settings via command-line arguments
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.NovaTuneApp_AppHost>(
            [
                "--environment=Testing",
                "--JWT_SIGNING_KEY=test-signing-key-must-be-at-least-32-characters-long-for-auth-tests",
                "--Jwt:Issuer=https://test.novatune.example",
                "--Jwt:Audience=novatune-test-api",
                "--Jwt:AccessTokenExpirationMinutes=15",
                "--Jwt:RefreshTokenExpirationMinutes=60",
                // High rate limits for tests
                "--RateLimiting:Auth:LoginPerIp:PermitLimit=1000",
                "--RateLimiting:Auth:LoginPerIp:WindowMinutes=1",
                "--RateLimiting:Auth:LoginPerAccount:PermitLimit=1000",
                "--RateLimiting:Auth:LoginPerAccount:WindowMinutes=1",
                "--RateLimiting:Auth:RegisterPerIp:PermitLimit=1000",
                "--RateLimiting:Auth:RegisterPerIp:WindowMinutes=1",
                "--RateLimiting:Auth:RefreshPerIp:PermitLimit=1000",
                "--RateLimiting:Auth:RefreshPerIp:WindowMinutes=1"
            ]);

        _app = await appHost.BuildAsync();

        // Increase startup timeout for container orchestration
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();

        await _app.StartAsync();

        // Wait for the API service to be ready
        await resourceNotificationService.WaitForResourceAsync("apiservice", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(5));

        // Get the API service HTTP client
        _client = _app.CreateHttpClient("apiservice");

        // Get RavenDB connection for test utilities
        var connectionString = await _app.GetConnectionStringAsync("novatune");

        // RavenDB connection string from Aspire is the URL to the database
        _documentStore = new DocumentStore
        {
            Urls = [connectionString!],
            Database = "novatune"
        };
        _documentStore.Initialize();
    }

    public async Task DisposeAsync()
    {
        try
        {
            _documentStore?.Dispose();
            _client?.Dispose();
        }
        finally
        {
            if (_app != null)
            {
                await _app.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Gets a user by email for test verification.
    /// </summary>
    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        using var session = _documentStore.OpenAsyncSession();
        return await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Updates a user's status for test scenarios.
    /// </summary>
    public async Task UpdateUserStatusAsync(string email, UserStatus status)
    {
        using var session = _documentStore.OpenAsyncSession();
        var user = await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();

        if (user != null)
        {
            user.Status = status;
            await session.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gets the count of active tokens for a user.
    /// </summary>
    public async Task<int> GetActiveTokenCountAsync(string userId)
    {
        using var session = _documentStore.OpenAsyncSession();
        return await session.Query<RefreshToken>()
            .Where(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .CountAsync();
    }

    /// <summary>
    /// Clears all test data from the database.
    /// </summary>
    public async Task ClearDataAsync()
    {
        using var session = _documentStore.OpenAsyncSession();

        // Delete all ApplicationUsers
        var users = await session.Query<ApplicationUser>().ToListAsync();
        foreach (var user in users)
            session.Delete(user);

        // Delete all RefreshTokens
        var tokens = await session.Query<RefreshToken>().ToListAsync();
        foreach (var token in tokens)
            session.Delete(token);

        await session.SaveChangesAsync();
    }
}
