using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Serilog;

namespace NovaTuneApp.Tests;

public class IntegrationTestsApiFactory : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private HttpClient _client = null!;
    private IDocumentStore _documentStore = null!;

    public HttpClient Client => _client;

    /// <summary>
    /// Creates a new HttpClient instance for tests that need their own client.
    /// </summary>
    public HttpClient CreateClient() => _app.CreateHttpClient("apiservice");

    public async Task InitializeAsync()
    {
        // Reset Serilog before creating the factory to avoid disposed provider references
        Log.CloseAndFlush();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        // Configure test-specific settings via command-line arguments
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.NovaTuneApp_AppHost>(
            [
                "--environment=Testing",
                // Testing runs should not require production secrets (e.g., cache encryption keys)
                "--NovaTune:CacheEncryption:Enabled=false",
                "--NovaTune:TopicPrefix=testing",
                "--Kafka:TopicPrefix=test",
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

        // Start with timeout to fail fast instead of hanging indefinitely
        using var startupCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await _app.StartAsync(startupCts.Token);
        }
        catch (OperationCanceledException) when (startupCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Aspire app startup exceeded 3 minutes. Common causes: a container runtime (Docker/Podman) is not running, first-time image pulls are slow, or the API service is crash-looping due to missing configuration (e.g., NovaTune:CacheEncryption enabled without a key).");
        }

        // Wait for the API service to be healthy (not just running)
        // This ensures all health checks pass before tests run
        using var healthCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("apiservice", healthCts.Token);

        // Get the API service HTTP client
        _client = _app.CreateHttpClient("apiservice");

        // Get RavenDB connection for test utilities
        // Connection string format: "URL=http://localhost:8080;Database=novatune"
        var connectionString = await _app.GetConnectionStringAsync("novatune");
        var parts = connectionString!.Split(';')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => p[1]);

        _documentStore = new DocumentStore
        {
            Urls = [parts["URL"]],
            Database = parts["Database"]
        };
        _documentStore.Initialize();

        // Ensure the database exists (create if it doesn't)
        EnsureDatabaseExists(_documentStore, parts["Database"]);
    }

    /// <summary>
    /// Ensures the specified database exists, creating it if necessary.
    /// </summary>
    private static void EnsureDatabaseExists(IDocumentStore store, string databaseName)
    {
        try
        {
            store.Maintenance.ForDatabase(databaseName).Send(new GetStatisticsOperation());
        }
        catch (DatabaseDoesNotExistException)
        {
            store.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(databaseName)));
        }
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
            .Customize(x => x.WaitForNonStaleResults())
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
            .Customize(x => x.WaitForNonStaleResults())
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();

        if (user != null)
        {
            user.Status = status;
            // Ensure indexes are updated before returning so API sees the change
            session.Advanced.WaitForIndexesAfterSaveChanges();
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
            .Customize(x => x.WaitForNonStaleResults())
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
        var users = await session.Query<ApplicationUser>()
            .Customize(x => x.WaitForNonStaleResults())
            .ToListAsync();
        foreach (var user in users)
            session.Delete(user);

        // Delete all RefreshTokens
        var tokens = await session.Query<RefreshToken>()
            .Customize(x => x.WaitForNonStaleResults())
            .ToListAsync();
        foreach (var token in tokens)
            session.Delete(token);

        // Wait for indexes to process the deletions before returning
        // This ensures subsequent test operations see a clean database
        session.Advanced.WaitForIndexesAfterSaveChanges();
        await session.SaveChangesAsync();
    }
}
