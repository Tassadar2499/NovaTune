# Step 8: Required Changes to AuthIntegrationTests

## Overview

Update specific test patterns to work with real infrastructure.

## Change Pattern 1: GetUserByEmail → GetUserByEmailAsync

### Before
```csharp
var user = _factory.GetUserByEmail("activeuser@example.com");
user.ShouldNotBeNull();
user.Status.ShouldBe(UserStatus.Active);
```

### After
```csharp
var user = await _factory.GetUserByEmailAsync("activeuser@example.com");
user.ShouldNotBeNull();
user.Status.ShouldBe(UserStatus.Active);
```

### Affected Tests
- `Register_Should_create_user_with_active_status`

## Change Pattern 2: Direct Status Modification → UpdateUserStatusAsync

### Before
```csharp
var user = _factory.GetUserByEmail("disabled@example.com");
user!.Status = UserStatus.Disabled;
```

### After
```csharp
await _factory.UpdateUserStatusAsync("disabled@example.com", UserStatus.Disabled);
```

### Affected Tests
- `Login_Should_return_403_for_disabled_user`
- `Refresh_Should_return_403_for_disabled_user`

## Change Pattern 3: CreateClient → Client Property

### Before
```csharp
_client = _factory.CreateClient();
```

### After
```csharp
_client = _factory.Client;
```

### Location
- `InitializeAsync()` method

## Complete Test Updates

### Register_Should_create_user_with_active_status

```csharp
[Fact]
public async Task Register_Should_create_user_with_active_status()
{
    var request = new RegisterRequest("activeuser@example.com", "Active User", "SecurePassword123!");

    await _client.PostAsJsonAsync("/auth/register", request);

    var user = await _factory.GetUserByEmailAsync("activeuser@example.com");
    user.ShouldNotBeNull();
    user.Status.ShouldBe(UserStatus.Active);
}
```

### Login_Should_return_403_for_disabled_user

```csharp
[Fact]
public async Task Login_Should_return_403_for_disabled_user()
{
    await _client.PostAsJsonAsync("/auth/register",
        new RegisterRequest("disabled@example.com", "Disabled User", "SecurePassword123!"));

    // Disable the user via test utility
    await _factory.UpdateUserStatusAsync("disabled@example.com", UserStatus.Disabled);

    var loginRequest = new LoginRequest("disabled@example.com", "SecurePassword123!");
    var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
    problem.ShouldNotBeNull();
    problem.Type!.ShouldContain("account-disabled");
}
```

### Refresh_Should_return_403_for_disabled_user

```csharp
[Fact]
public async Task Refresh_Should_return_403_for_disabled_user()
{
    await _client.PostAsJsonAsync("/auth/register",
        new RegisterRequest("disabledrefresh@example.com", "User", "SecurePassword123!"));

    var loginResponse = await _client.PostAsJsonAsync("/auth/login",
        new LoginRequest("disabledrefresh@example.com", "SecurePassword123!"));
    var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);

    // Disable user after login via test utility
    await _factory.UpdateUserStatusAsync("disabledrefresh@example.com", UserStatus.Disabled);

    var refreshResponse = await _client.PostAsJsonAsync("/auth/refresh",
        new RefreshRequest(tokens!.RefreshToken));

    refreshResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
}
```

## Acceptance Criteria

- [ ] All synchronous helper calls converted to async
- [ ] Direct object modification replaced with utility methods
- [ ] All tests compile without errors
- [ ] Test logic remains unchanged
