# Plan: Fix AuthApiFactory to Use Real Infrastructure

## Overview

Convert `AuthApiFactory` from using NSubstitute mocks and in-memory stores to using real containerized infrastructure via Aspire.Hosting.Testing.

## Current State

The current `AuthApiFactory` (src/integration_tests/NovaTuneApp.IntegrationTests/AuthApiFactory.cs):
- Uses `WebApplicationFactory<Program>` with heavy mocking
- In-memory `Dictionary` stores for users and tokens
- NSubstitute mocks for `IUserStore`, `IRefreshTokenRepository`, `ICacheService`, `IMessageProducerService`
- Removes all `IHostedService` registrations
- Provides test helper methods that depend on in-memory storage (`GetUserByEmail`, `GetActiveTokenCount`)

## Target State

Use Aspire.Hosting.Testing to spin up real containers:
- RavenDB for user/token persistence
- Redis/Garnet for caching
- Kafka/Redpanda for messaging

## Implementation Steps

### Step 1: Create AspireAuthApiFactory

Create a new factory class that uses Aspire testing infrastructure:

**File**: `src/integration_tests/NovaTuneApp.IntegrationTests/AspireAuthApiFactory.cs`

Key components:
1. Use `DistributedApplicationTestingBuilder.CreateAsync<Projects.NovaTuneApp_AppHost>()` to build the Aspire app
2. Get `HttpClient` for the API service from the distributed app
3. Configure JWT settings for tests via environment variables
4. Set high rate limits for test stability

### Step 2: Container Configuration

The AppHost already configures all necessary infrastructure:
- `builder.AddRavenDB("ravendb")` with database `novatune`
- `builder.AddRedis("cache")` (Garnet)
- `builder.AddKafka("messaging")` (Redpanda)

For tests, we need:
- Per-test database isolation (use unique database names or clean data between tests)
- Appropriate wait strategies for container readiness

### Step 3: Implement Test Data Management

Replace in-memory helper methods with real database queries:

| Current Method | Replacement Approach |
|----------------|---------------------|
| `GetUserByEmail(email)` | Query RavenDB via `IAsyncDocumentSession` |
| `GetActiveTokenCount(userId)` | Query RavenDB for active tokens |
| `ClearData()` | Delete all documents in test database |

**Recommended isolation strategy**: Clean database between tests via `InitializeAsync`.

### Step 4: Update AuthIntegrationTests

Changes needed:
1. Replace `AuthApiFactory` usage pattern
2. Replace `_factory.GetUserByEmail()` calls with async DB queries
3. Update `InitializeAsync` to wait for containers to be ready
4. Update `DisposeAsync` to properly clean up resources

### Step 5: Handle Test-Specific User State Manipulation

Tests that modify user state (e.g., disabling a user for `Login_Should_return_403_for_disabled_user`):

**Recommended approach**: Provide test utility methods that access the DB session directly:
- `GetUserByEmailAsync(email)` - query user from RavenDB
- `UpdateUserStatusAsync(email, status)` - modify user status in RavenDB
- `GetActiveTokenCountAsync(userId)` - count active tokens in RavenDB
- `ClearDataAsync()` - delete all test data

### Step 6: Implementation Details

#### 6.1 AspireAuthApiFactory Structure

```csharp
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
        appHost.Services.Configure<...>(...);

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
        await _app.DisposeAsync();
    }

    // Test utilities using real DB
    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        using var session = _documentStore.OpenAsyncSession();
        return await session.Query<ApplicationUser>()
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();
    }

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

    public async Task<int> GetActiveTokenCountAsync(string userId)
    {
        using var session = _documentStore.OpenAsyncSession();
        return await session.Query<RefreshToken>()
            .Where(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .CountAsync();
    }

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

#### 6.2 Test Configuration

Pass test JWT settings via appHost configuration:
```csharp
appHost.Configuration["JWT_SIGNING_KEY"] = "test-signing-key-must-be-at-least-32-characters-long";
appHost.Configuration["Jwt:Issuer"] = "https://test.novatune.example";
appHost.Configuration["Jwt:Audience"] = "novatune-test-api";
appHost.Configuration["Jwt:AccessTokenExpirationMinutes"] = "15";
appHost.Configuration["Jwt:RefreshTokenExpirationMinutes"] = "60";
// High rate limits for tests
appHost.Configuration["RateLimiting:Auth:LoginPerIp:PermitLimit"] = "1000";
// ... other rate limit settings
```

#### 6.3 Updated Test Pattern

```csharp
[Collection("Auth Integration Tests")]
public class AuthIntegrationTests : IAsyncLifetime
{
    private AspireAuthApiFactory _factory = null!;
    private HttpClient _client = null!;

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

    [Fact]
    public async Task Login_Should_return_403_for_disabled_user()
    {
        // Register user via API
        await _client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("disabled@example.com", "Disabled User", "SecurePassword123!"));

        // Disable user via test utility (direct DB access)
        await _factory.UpdateUserStatusAsync("disabled@example.com", UserStatus.Disabled);

        // Test login
        var loginRequest = new LoginRequest("disabled@example.com", "SecurePassword123!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
```

### Step 7: File Changes Summary

| File | Action |
|------|--------|
| `AuthApiFactory.cs` | Rewrite to use Aspire.Hosting.Testing |
| `AuthIntegrationTests.cs` | Update helper method calls to async, adjust for real infrastructure |
| `TestCollections.cs` | Keep sequential execution (container resource sharing) |
| `NovaTuneApp.IntegrationTests.csproj` | Verify Aspire.Hosting.Testing reference (already present) |

### Step 8: Required Changes to AuthIntegrationTests

Update these specific test patterns:

1. **`_factory.GetUserByEmail(email)`** → **`await _factory.GetUserByEmailAsync(email)`**
   - Affected tests: `Register_Should_create_user_with_active_status`, `Login_Should_return_403_for_disabled_user`, `Refresh_Should_return_403_for_disabled_user`

2. **`user!.Status = UserStatus.Disabled`** → **`await _factory.UpdateUserStatusAsync(email, UserStatus.Disabled)`**
   - Affected tests: `Login_Should_return_403_for_disabled_user`, `Refresh_Should_return_403_for_disabled_user`

3. **`_factory.GetActiveTokenCount(userId)`** → **`await _factory.GetActiveTokenCountAsync(userId)`**
   - Currently not used in tests, but keep for potential future use

### Step 9: Considerations

1. **Test execution time**: Real containers are slower than mocks (~30-60s startup per test class)
2. **Resource cleanup**: Ensure containers are properly cleaned up on test failure
3. **CI/CD requirements**: Docker must be available in test environment
4. **Parallel execution**: Keep sequential execution to avoid port/resource conflicts
5. **Rate limiting**: Maintain high rate limits in test configuration
6. **Serilog handling**: May still need bootstrap logger handling due to container startup

### Step 10: Verification

After implementation, verify:
1. All 14 existing tests pass
2. No NSubstitute references remain in AuthApiFactory
3. No in-memory Dictionary storage
4. Real data persists in RavenDB during test execution
5. Containers start and stop cleanly

## Acceptance Criteria

- [ ] AuthApiFactory uses real RavenDB container for user/token storage
- [ ] AuthApiFactory uses real Redis/Garnet container for caching
- [ ] AuthApiFactory uses real Kafka/Redpanda container for messaging
- [ ] No NSubstitute mocks for infrastructure services (IUserStore, IRefreshTokenRepository, ICacheService, IMessageProducerService)
- [ ] No in-memory Dictionary stores
- [ ] All existing AuthIntegrationTests pass
- [ ] Test utilities provide async DB access for test-specific operations
- [ ] Proper cleanup between tests
- [ ] Tests tagged with appropriate trait for CI filtering
