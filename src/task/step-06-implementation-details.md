# Step 6: Implementation Details

## 6.1 Complete AspireAuthApiFactory Structure

```csharp
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;
using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTuneApp.Tests;

/// <summary>
/// Aspire-based test factory for auth integration tests.
/// Uses real containerized infrastructure (RavenDB, Redis, Kafka).
/// </summary>
public class AspireAuthApiFactory : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private HttpClient _client = null!;
    private IDocumentStore _documentStore = null!;

    public HttpClient Client => _client;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.NovaTuneApp_AppHost>();

        // Configure test-specific settings
        appHost.Services.AddSingleton<IConfiguration>(sp =>
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT_SIGNING_KEY"] = "test-signing-key-must-be-at-least-32-characters-long-for-auth-tests",
                    ["Jwt:Issuer"] = "https://test.novatune.example",
                    ["Jwt:Audience"] = "novatune-test-api",
                    ["Jwt:AccessTokenExpirationMinutes"] = "15",
                    ["Jwt:RefreshTokenExpirationMinutes"] = "60",
                    // High rate limits for tests
                    ["RateLimiting:Auth:LoginPerIp:PermitLimit"] = "1000",
                    ["RateLimiting:Auth:LoginPerIp:WindowMinutes"] = "1",
                    ["RateLimiting:Auth:LoginPerAccount:PermitLimit"] = "1000",
                    ["RateLimiting:Auth:LoginPerAccount:WindowMinutes"] = "1",
                    ["RateLimiting:Auth:RegisterPerIp:PermitLimit"] = "1000",
                    ["RateLimiting:Auth:RegisterPerIp:WindowMinutes"] = "1",
                    ["RateLimiting:Auth:RefreshPerIp:PermitLimit"] = "1000",
                    ["RateLimiting:Auth:RefreshPerIp:WindowMinutes"] = "1"
                })
                .Build();
            return config;
        });

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Get the API service HTTP client
        _client = _app.CreateHttpClient("apiservice");

        // Get RavenDB connection for test utilities
        var connectionString = await _app.GetConnectionStringAsync("novatune");
        _documentStore = new DocumentStore
        {
            Urls = [connectionString],
            Database = "NovaTune"
        };
        _documentStore.Initialize();
    }

    public async Task DisposeAsync()
    {
        _documentStore?.Dispose();
        _client?.Dispose();
        await _app.DisposeAsync();
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
```

## 6.2 Test Configuration

The test configuration is injected via the `IConfiguration` singleton added to `appHost.Services`.

Key settings:
- **JWT_SIGNING_KEY**: Must be at least 32 characters for HMAC-SHA256
- **Jwt:Issuer/Audience**: Test-specific values
- **RateLimiting**: High limits to prevent test interference

## 6.3 Updated Test Pattern

```csharp
[Collection("Auth Integration Tests")]
public class AuthIntegrationTests : IAsyncLifetime
{
    private AspireAuthApiFactory _factory = null!;
    private HttpClient _client = null!;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuthIntegrationTests()
    {
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task InitializeAsync()
    {
        _factory = new AspireAuthApiFactory();
        await _factory.InitializeAsync();
        _client = _factory.Client;
        // Clean any leftover data from previous runs
        await _factory.ClearDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    // Tests use _client for HTTP and _factory for test utilities
}
```

## Acceptance Criteria

- [ ] Factory initializes all containers successfully
- [ ] Configuration is properly injected
- [ ] All test utility methods work correctly
- [ ] Resources are properly disposed
