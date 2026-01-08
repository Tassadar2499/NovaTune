using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Auth;

namespace NovaTuneApp.Tests;

/// <summary>
/// Integration tests for the authentication flow.
/// Tests registration, login, refresh token rotation, logout, and error scenarios.
/// Uses Aspire testing infrastructure with real containers.
/// The factory is shared across all tests via xUnit collection fixture.
/// </summary>
[Trait("Category", "Aspire")]
[Collection("Integration Tests")]
public class AuthIntegrationTests : IAsyncLifetime
{
    private readonly IntegrationTestsApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuthIntegrationTests(IntegrationTestsApiFactory factory)
    {
        _factory = factory;
        _client = factory.Client;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task InitializeAsync()
    {
        // Clean any leftover data from previous tests
        await _factory.ClearDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ========================================================================
    // Registration Tests
    // ========================================================================

    [Fact]
    public async Task Register_Should_return_201_with_user_data()
    {
        var request = new RegisterRequest("newuser@example.com", "New User", "SecurePassword123!");

        var response = await _client.PostAsJsonAsync("/auth/register", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>(_jsonOptions);
        user.ShouldNotBeNull();
        user.Email.ShouldBe("newuser@example.com");
        user.DisplayName.ShouldBe("New User");
        user.UserId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_Should_create_user_with_active_status()
    {
        var request = new RegisterRequest("activeuser@example.com", "Active User", "SecurePassword123!");

        await _client.PostAsJsonAsync("/auth/register", request);

        var user = await _factory.GetUserByEmailAsync("activeuser@example.com");
        user.ShouldNotBeNull();
        user.Status.ShouldBe(UserStatus.Active);
    }

    [Fact]
    public async Task Register_Should_return_409_for_duplicate_email()
    {
        var request = new RegisterRequest("duplicate@example.com", "User One", "SecurePassword123!");
        await _client.PostAsJsonAsync("/auth/register", request);

        var duplicateRequest = new RegisterRequest("duplicate@example.com", "User Two", "AnotherPassword456!");
        var response = await _client.PostAsJsonAsync("/auth/register", duplicateRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("email-exists");
    }

    // ========================================================================
    // Login Tests
    // ========================================================================

    [Fact]
    public async Task Login_Should_return_200_with_tokens()
    {
        // Register first
        await _client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("logintest@example.com", "Login Test", "SecurePassword123!"));

        // Login
        var loginRequest = new LoginRequest("logintest@example.com", "SecurePassword123!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
        authResponse.ShouldNotBeNull();
        authResponse.AccessToken.ShouldNotBeNullOrWhiteSpace();
        authResponse.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        authResponse.ExpiresIn.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Login_Should_return_401_for_wrong_password()
    {
        await _client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("wrongpass@example.com", "User", "CorrectPassword123!"));

        var loginRequest = new LoginRequest("wrongpass@example.com", "WrongPassword456!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("invalid-credentials");
    }

    [Fact]
    public async Task Login_Should_return_401_for_nonexistent_user()
    {
        var loginRequest = new LoginRequest("nonexistent@example.com", "AnyPassword123!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("invalid-credentials");
    }

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

    // ========================================================================
    // Refresh Token Tests
    // ========================================================================

    [Fact]
    public async Task Refresh_Should_rotate_tokens()
    {
        await _client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("refresh@example.com", "Refresh User", "SecurePassword123!"));

        var loginResponse = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("refresh@example.com", "SecurePassword123!"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);

        var refreshResponse = await _client.PostAsJsonAsync("/auth/refresh",
            new RefreshRequest(tokens!.RefreshToken));

        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
        newTokens.ShouldNotBeNull();
        newTokens.RefreshToken.ShouldNotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_Should_return_401_for_invalid_token()
    {
        var response = await _client.PostAsJsonAsync("/auth/refresh",
            new RefreshRequest("invalid-refresh-token"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("invalid-token");
    }

    [Fact]
    public async Task Refresh_Should_return_401_for_already_used_token()
    {
        await _client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("usedtoken@example.com", "Used Token User", "SecurePassword123!"));

        var loginResponse = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("usedtoken@example.com", "SecurePassword123!"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);

        // First refresh - should succeed
        var firstRefresh = await _client.PostAsJsonAsync("/auth/refresh",
            new RefreshRequest(tokens!.RefreshToken));
        firstRefresh.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Second refresh with same token - should fail (one-time use)
        var secondRefresh = await _client.PostAsJsonAsync("/auth/refresh",
            new RefreshRequest(tokens.RefreshToken));
        secondRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

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

    [Fact]
    public async Task Logout_Should_return_401_without_auth_header()
    {
        var response = await _client.PostAsJsonAsync("/auth/logout",
            new RefreshRequest("any-token"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ========================================================================
    // RFC 7807 Problem Details Tests
    // ========================================================================

    [Fact]
    public async Task Errors_Should_include_traceId()
    {
        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("nonexistent@example.com", "password"));

        var content = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<JsonElement>(content);

        problem.TryGetProperty("traceId", out var traceId).ShouldBeTrue();
        traceId.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Errors_Should_include_instance_path()
    {
        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest("nonexistent@example.com", "password"));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Instance.ShouldBe("/auth/login");
    }
}
