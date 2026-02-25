using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Endpoints;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Auth;

namespace NovaTuneApp.Tests;

/// <summary>
/// Integration tests for streaming URL functionality.
/// Tests per 11-test-strategy.md requirements:
/// - End-to-end stream URL flow (cache miss → presign → cache)
/// - Cache invalidation on track deletion
/// - Cache invalidation on logout
/// - Rate limiting enforcement
/// - Range request verification (client → MinIO)
///
/// Uses Aspire testing infrastructure with real containers.
/// </summary>
[Trait("Category", "Aspire")]
[Collection("Integration Tests")]
public class StreamingIntegrationTests : IAsyncLifetime
{
    private readonly IntegrationTestsApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public StreamingIntegrationTests(IntegrationTestsApiFactory factory)
    {
        _factory = factory;
        _client = factory.Client;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task InitializeAsync()
    {
        await _factory.ClearDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ========================================================================
    // End-to-End Stream URL Flow Tests
    // ========================================================================

    [Fact]
    public async Task Stream_endpoint_Should_require_authentication()
    {
        // Arrange - no auth header

        // Act
        var response = await _client.PostAsync("/tracks/01HQXYZ123456789ABCDEFGH/stream", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Stream_endpoint_Should_return_400_for_invalid_track_id()
    {
        // Arrange - authenticate
        var accessToken = await GetAccessTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/tracks/invalid-track-id/stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("invalid-track-id");
    }

    [Fact]
    public async Task Stream_endpoint_Should_return_404_for_nonexistent_track()
    {
        // Arrange - authenticate
        var accessToken = await GetAccessTokenAsync();
        var ulid = Ulid.NewUlid(); // Generate a valid ULID that doesn't exist
        var request = new HttpRequestMessage(HttpMethod.Post, $"/tracks/{ulid}/stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("track-not-found");
    }

    // NOTE: The following tests require a track to exist in the system.
    // Full end-to-end tests would need the upload pipeline to be working.
    // These are documented as the integration test scenarios per 11-test-strategy.md.

    // ========================================================================
    // Cache Flow Tests (Require Track Setup)
    // ========================================================================

    /// <summary>
    /// Tests the end-to-end stream URL flow: cache miss → presign → cache.
    /// Requires: A track uploaded and processed to Ready status.
    /// </summary>
    /// <remarks>
    /// This test validates:
    /// 1. First request generates a presigned URL (cache miss)
    /// 2. URL is returned with correct format
    /// 3. Second request returns faster (cache hit - implicit through timing or headers)
    /// 4. URL expiration is within expected range
    /// </remarks>
    [Fact(Skip = "Requires track upload pipeline - implement when upload is ready")]
    public async Task Stream_url_flow_Should_cache_presigned_url()
    {
        // Arrange
        // var trackId = await UploadAndProcessTrackAsync();
        // var accessToken = await GetAccessTokenAsync();

        // Act - First request (cache miss)
        // var firstResponse = await GetStreamUrlAsync(trackId, accessToken);

        // Act - Second request (cache hit)
        // var secondResponse = await GetStreamUrlAsync(trackId, accessToken);

        // Assert
        // firstResponse.StreamUrl.ShouldNotBeNullOrEmpty();
        // firstResponse.StreamUrl.ShouldBe(secondResponse.StreamUrl);
        // firstResponse.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    // ========================================================================
    // Cache Invalidation Tests
    // ========================================================================

    /// <summary>
    /// Tests that cache is invalidated when a track is deleted.
    /// </summary>
    [Fact(Skip = "Requires track upload and deletion pipeline")]
    public async Task Cache_Should_be_invalidated_on_track_deletion()
    {
        // Arrange
        // var trackId = await UploadAndProcessTrackAsync();
        // var accessToken = await GetAccessTokenAsync();

        // Act - Get stream URL (populates cache)
        // await GetStreamUrlAsync(trackId, accessToken);

        // Act - Delete track
        // await DeleteTrackAsync(trackId, accessToken);

        // Assert - Stream URL should fail (track deleted)
        // var response = await TryGetStreamUrlAsync(trackId, accessToken);
        // response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests that all user caches are invalidated on logout.
    /// </summary>
    [Fact(Skip = "Requires track upload pipeline and logout cache invalidation")]
    public async Task Cache_Should_be_invalidated_on_logout()
    {
        // Arrange
        // var trackId = await UploadAndProcessTrackAsync();
        // var accessToken = await GetAccessTokenAsync();

        // Act - Get stream URL (populates cache)
        // var firstUrl = await GetStreamUrlAsync(trackId, accessToken);

        // Act - Logout all sessions
        // await LogoutAllAsync(accessToken);

        // Act - Login again and get new stream URL
        // var newAccessToken = await GetAccessTokenAsync();
        // var secondUrl = await GetStreamUrlAsync(trackId, newAccessToken);

        // Assert - URLs should be different (cache was invalidated)
        // Note: Due to random nonces in presigned URLs, URLs may differ anyway
        // This test would verify cache miss metrics or timing
    }

    // ========================================================================
    // Rate Limiting Tests
    // ========================================================================

    /// <summary>
    /// Tests that rate limiting is enforced on stream URL requests.
    /// </summary>
    [Fact(Skip = "Requires rate limit configuration for testing")]
    public async Task Stream_endpoint_Should_enforce_rate_limiting()
    {
        // Arrange
        // Configure test with low rate limit (e.g., 5 per minute)
        // var accessToken = await GetAccessTokenAsync();
        // var trackId = await UploadAndProcessTrackAsync();

        // Act - Make requests exceeding rate limit
        // for (int i = 0; i < 10; i++)
        // {
        //     var response = await TryGetStreamUrlAsync(trackId, accessToken);
        //     if (i < 5) response.StatusCode.ShouldBe(HttpStatusCode.OK);
        //     else response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        // }

        // Assert - Later requests should be rate limited
        // var finalResponse = await TryGetStreamUrlAsync(trackId, accessToken);
        // finalResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // ========================================================================
    // Range Request Tests
    // ========================================================================

    /// <summary>
    /// Tests that presigned URLs support HTTP range requests for seeking.
    /// </summary>
    [Fact(Skip = "Requires track upload pipeline and MinIO access")]
    public async Task Presigned_url_Should_support_range_requests()
    {
        // Arrange
        // var trackId = await UploadAndProcessTrackAsync();
        // var accessToken = await GetAccessTokenAsync();
        // var streamResponse = await GetStreamUrlAsync(trackId, accessToken);

        // Act - Make range request to presigned URL
        // using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, streamResponse.StreamUrl);
        // rangeRequest.Headers.Range = new RangeHeaderValue(0, 1023); // First 1KB
        // var rangeResponse = await _client.SendAsync(rangeRequest);

        // Assert
        // rangeResponse.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        // rangeResponse.Content.Headers.ContentRange.ShouldNotBeNull();
        // streamResponse.SupportsRangeRequests.ShouldBeTrue();
    }

    /// <summary>
    /// Tests that response includes correct content type and size metadata.
    /// </summary>
    [Fact(Skip = "Requires track upload pipeline")]
    public async Task Stream_response_Should_include_metadata()
    {
        // Arrange
        // var trackId = await UploadAndProcessTrackAsync("audio/mpeg", 5_000_000);
        // var accessToken = await GetAccessTokenAsync();

        // Act
        // var response = await GetStreamUrlAsync(trackId, accessToken);

        // Assert
        // response.ContentType.ShouldBe("audio/mpeg");
        // response.FileSizeBytes.ShouldBe(5_000_000);
        // response.SupportsRangeRequests.ShouldBeTrue();
    }

    // ========================================================================
    // Error Handling Tests
    // ========================================================================

    [Fact]
    public async Task Stream_endpoint_Should_return_409_for_processing_track()
    {
        // Arrange - create a user and seed a track with Processing status
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"stream-processing-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("Processing Track",
            userId: userId, status: TrackStatus.Processing);

        // Act - attempt to stream a track that is still processing
        var response = await client.PostAsync($"/tracks/{trackId}/stream", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("track-not-ready");

        client.Dispose();
    }

    [Fact]
    public async Task Stream_endpoint_Should_return_403_for_other_users_track()
    {
        // Arrange - create two users and seed a Ready track owned by user A
        var (clientA, userA) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"stream-a-{Guid.NewGuid():N}@test.com");
        var (clientB, userB) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"stream-b-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("User A Track", userId: userA);

        // Act - user B tries to stream user A's track
        var response = await clientB.PostAsync($"/tracks/{trackId}/stream", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("forbidden");

        clientA.Dispose();
        clientB.Dispose();
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private async Task<string> GetAccessTokenAsync(string email = "streamtest@example.com")
    {
        // Register and login to get access token
        var registerRequest = new RegisterRequest(email, "Stream Test User", "SecurePassword123!");
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new LoginRequest(email, "SecurePassword123!");
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);

        return authResponse!.AccessToken;
    }

    private async Task<StreamResponse?> GetStreamUrlAsync(string trackId, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/tracks/{trackId}/stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<StreamResponse>(_jsonOptions);
    }

    private async Task<HttpResponseMessage> TryGetStreamUrlAsync(string trackId, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/tracks/{trackId}/stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request);
    }
}
