# Step 3: Implement Test Data Management

## Objective

Replace in-memory helper methods with real database queries for test data management.

## Method Mapping

| Current Method | Replacement Method | Description |
|----------------|-------------------|-------------|
| `GetUserByEmail(email)` | `GetUserByEmailAsync(email)` | Query RavenDB via `IAsyncDocumentSession` |
| `GetActiveTokenCount(userId)` | `GetActiveTokenCountAsync(userId)` | Query RavenDB for active tokens |
| `ClearData()` | `ClearDataAsync()` | Delete all documents in test database |

## Implementation

Add these methods to `AspireAuthApiFactory`:

```csharp
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
```

## Required Imports

```csharp
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Session;
using NovaTuneApp.ApiService.Models.Identity;
```

## Isolation Strategy

Call `ClearDataAsync()` in test `InitializeAsync()` to ensure clean state:

```csharp
public async Task InitializeAsync()
{
    _factory = new AspireAuthApiFactory();
    await _factory.InitializeAsync();
    _client = _factory.Client;
    // Clean any leftover data from previous runs
    await _factory.ClearDataAsync();
}
```

## Acceptance Criteria

- [ ] `GetUserByEmailAsync` returns user from RavenDB
- [ ] `GetActiveTokenCountAsync` returns correct count from RavenDB
- [ ] `ClearDataAsync` removes all test data
- [ ] Tests start with clean database state
