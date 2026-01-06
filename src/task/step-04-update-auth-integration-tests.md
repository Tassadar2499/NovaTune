# Step 4: Update AuthIntegrationTests

## Objective

Update the test class to use the new `AspireAuthApiFactory` with real infrastructure.

## Changes Required

### 1. Update Class Structure

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
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
```

### 2. Update Helper Method Calls

Replace synchronous calls with async:

**Before:**
```csharp
var user = _factory.GetUserByEmail("activeuser@example.com");
```

**After:**
```csharp
var user = await _factory.GetUserByEmailAsync("activeuser@example.com");
```

### 3. Update User State Manipulation

**Before:**
```csharp
var user = _factory.GetUserByEmail("disabled@example.com");
user!.Status = UserStatus.Disabled;
```

**After:**
```csharp
await _factory.UpdateUserStatusAsync("disabled@example.com", UserStatus.Disabled);
```

## Affected Tests

| Test Method | Changes Needed |
|-------------|----------------|
| `Register_Should_create_user_with_active_status` | `GetUserByEmail` → `GetUserByEmailAsync` |
| `Login_Should_return_403_for_disabled_user` | `GetUserByEmail` + status change → `UpdateUserStatusAsync` |
| `Refresh_Should_return_403_for_disabled_user` | `GetUserByEmail` + status change → `UpdateUserStatusAsync` |

## No Changes Needed

These tests only use HTTP client and don't access factory helper methods:
- `Register_Should_return_201_with_user_data`
- `Register_Should_return_409_for_duplicate_email`
- `Login_Should_return_200_with_tokens`
- `Login_Should_return_401_for_wrong_password`
- `Login_Should_return_401_for_nonexistent_user`
- `Refresh_Should_rotate_tokens`
- `Refresh_Should_return_401_for_invalid_token`
- `Refresh_Should_return_401_for_already_used_token`
- `Logout_Should_return_401_without_auth_header`
- `Errors_Should_include_traceId`
- `Errors_Should_include_instance_path`

## Acceptance Criteria

- [ ] All tests compile without errors
- [ ] `InitializeAsync` properly initializes factory and clears data
- [ ] `DisposeAsync` properly cleans up resources
- [ ] Helper method calls updated to async versions
