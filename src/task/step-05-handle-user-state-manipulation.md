# Step 5: Handle Test-Specific User State Manipulation

## Objective

Provide test utility methods for modifying user state directly in the database, replacing direct object manipulation.

## Problem

Current tests manipulate user state directly on in-memory objects:

```csharp
var user = _factory.GetUserByEmail("disabled@example.com");
user!.Status = UserStatus.Disabled;  // Direct modification
```

With real infrastructure, we need to persist these changes to the database.

## Solution

Add `UpdateUserStatusAsync` method to `AspireAuthApiFactory`:

```csharp
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
```

## Usage in Tests

**Before:**
```csharp
[Fact]
public async Task Login_Should_return_403_for_disabled_user()
{
    await _client.PostAsJsonAsync("/auth/register",
        new RegisterRequest("disabled@example.com", "Disabled User", "SecurePassword123!"));

    // Disable the user - OLD WAY
    var user = _factory.GetUserByEmail("disabled@example.com");
    user!.Status = UserStatus.Disabled;

    var loginRequest = new LoginRequest("disabled@example.com", "SecurePassword123!");
    var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
}
```

**After:**
```csharp
[Fact]
public async Task Login_Should_return_403_for_disabled_user()
{
    await _client.PostAsJsonAsync("/auth/register",
        new RegisterRequest("disabled@example.com", "Disabled User", "SecurePassword123!"));

    // Disable the user - NEW WAY
    await _factory.UpdateUserStatusAsync("disabled@example.com", UserStatus.Disabled);

    var loginRequest = new LoginRequest("disabled@example.com", "SecurePassword123!");
    var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
}
```

## Tests Requiring This Method

1. `Login_Should_return_403_for_disabled_user`
2. `Refresh_Should_return_403_for_disabled_user`

## Additional Utility Methods (Optional)

For future extensibility, consider adding:

```csharp
/// <summary>
/// Adds a role to a user for test scenarios.
/// </summary>
public async Task AddUserRoleAsync(string email, string role)
{
    using var session = _documentStore.OpenAsyncSession();
    var user = await session.Query<ApplicationUser>()
        .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
        .FirstOrDefaultAsync();

    if (user != null && !user.Roles.Contains(role))
    {
        user.Roles.Add(role);
        await session.SaveChangesAsync();
    }
}
```

## Acceptance Criteria

- [ ] `UpdateUserStatusAsync` persists status change to RavenDB
- [ ] Tests that disable users work correctly
- [ ] Changes are visible to subsequent API calls
