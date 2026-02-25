using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Models.Telemetry;

namespace NovaTuneApp.Tests;

/// <summary>
/// Integration tests for telemetry endpoints: POST /telemetry/playback and POST /telemetry/playback/batch.
/// Validates authentication, event type validation, track ID validation, timestamp bounds,
/// track access control, and batch processing semantics.
/// </summary>
[Collection("Integration Tests")]
[Trait("Category", "Aspire")]
public class TelemetryEndpointTests(IntegrationTestsApiFactory factory) : IAsyncLifetime
{
    private readonly IntegrationTestsApiFactory _factory = factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync() => await _factory.ClearDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ========================================================================
    // Helper methods
    // ========================================================================

    private static object MakePlaybackEvent(
        string eventType,
        string trackId,
        DateTimeOffset? clientTimestamp = null,
        double? positionSeconds = null,
        double? durationPlayedSeconds = null,
        string? sessionId = null,
        string? deviceId = null,
        string? clientVersion = null)
    {
        return new
        {
            EventType = eventType,
            TrackId = trackId,
            ClientTimestamp = clientTimestamp ?? DateTimeOffset.UtcNow,
            PositionSeconds = positionSeconds,
            DurationPlayedSeconds = durationPlayedSeconds,
            SessionId = sessionId,
            DeviceId = deviceId,
            ClientVersion = clientVersion
        };
    }

    private static object MakeBatchRequest(IEnumerable<object> events)
    {
        return new { Events = events.ToArray() };
    }

    // ========================================================================
    // 3.1 - Single Event Ingestion
    // ========================================================================

    // #1
    [Fact]
    public async Task Telemetry_Should_return_401_for_unauthenticated_requests()
    {
        // Arrange - use the shared unauthenticated client
        var client = _factory.Client;
        var trackId = Ulid.NewUlid().ToString();
        var request = MakePlaybackEvent("play_start", trackId);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // #2
    [Fact]
    public async Task IngestPlayback_Should_return_202_for_play_start_event()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("Test Track", userId: userId);
        var request = MakePlaybackEvent("play_start", trackId);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<TelemetryAcceptedResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Accepted.ShouldBeTrue();
        result.CorrelationId.ShouldNotBeNullOrWhiteSpace();

        client.Dispose();
    }

    // #3
    [Fact]
    public async Task IngestPlayback_Should_return_202_for_play_complete_event()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("Complete Track", userId: userId);
        var request = MakePlaybackEvent("play_complete", trackId, durationPlayedSeconds: 195.5);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<TelemetryAcceptedResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Accepted.ShouldBeTrue();
        result.CorrelationId.ShouldNotBeNullOrWhiteSpace();

        client.Dispose();
    }

    // #4
    [Fact]
    public async Task IngestPlayback_Should_return_202_with_all_optional_fields()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("Full Fields Track", userId: userId);
        var request = MakePlaybackEvent(
            "play_progress",
            trackId,
            positionSeconds: 42.5,
            durationPlayedSeconds: 42.5,
            sessionId: "session-abc-123",
            deviceId: "device-xyz-456",
            clientVersion: "1.2.3");

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<TelemetryAcceptedResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Accepted.ShouldBeTrue();
        result.CorrelationId.ShouldNotBeNullOrWhiteSpace();

        client.Dispose();
    }

    // #5
    [Fact]
    public async Task IngestPlayback_Should_return_400_for_invalid_event_type()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = Ulid.NewUlid().ToString();
        var request = MakePlaybackEvent("invalid_type", trackId);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(400);
        problem.Type!.ShouldContain("invalid-event-type");

        client.Dispose();
    }

    // #6
    [Fact]
    public async Task IngestPlayback_Should_return_400_for_invalid_track_id()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var request = MakePlaybackEvent("play_start", "not-a-ulid");

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(400);
        problem.Type!.ShouldContain("invalid-track-id");

        client.Dispose();
    }

    // #7
    [Fact]
    public async Task IngestPlayback_Should_return_400_for_negative_position()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = Ulid.NewUlid().ToString();
        var request = MakePlaybackEvent("play_start", trackId, positionSeconds: -1);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(400);
        problem.Type!.ShouldContain("validation-error");

        client.Dispose();
    }

    // #8
    [Fact]
    public async Task IngestPlayback_Should_return_400_for_negative_duration()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = Ulid.NewUlid().ToString();
        var request = MakePlaybackEvent("play_start", trackId, durationPlayedSeconds: -1);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(400);
        problem.Type!.ShouldContain("validation-error");

        client.Dispose();
    }

    // #9
    [Fact]
    public async Task IngestPlayback_Should_return_400_for_timestamp_too_old()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("Old Timestamp Track", userId: userId);
        var oldTimestamp = DateTimeOffset.UtcNow.AddHours(-25);
        var request = MakePlaybackEvent("play_start", trackId, clientTimestamp: oldTimestamp);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(400);
        problem.Type!.ShouldContain("invalid-timestamp");

        client.Dispose();
    }

    // #10
    [Fact]
    public async Task IngestPlayback_Should_return_400_for_timestamp_in_future()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("Future Timestamp Track", userId: userId);
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(10);
        var request = MakePlaybackEvent("play_start", trackId, clientTimestamp: futureTimestamp);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(400);
        problem.Type!.ShouldContain("invalid-timestamp");

        client.Dispose();
    }

    // #11
    [Fact]
    public async Task IngestPlayback_Should_return_403_for_nonexistent_track()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        // Use a valid ULID that does not correspond to any seeded track
        var nonexistentTrackId = Ulid.NewUlid().ToString();
        var request = MakePlaybackEvent("play_start", nonexistentTrackId);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(403);
        problem.Type!.ShouldContain("track-access-denied");

        client.Dispose();
    }

    // #12
    [Fact]
    public async Task IngestPlayback_Should_return_403_for_other_users_track()
    {
        // Arrange
        var (clientA, userA) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-a-{Guid.NewGuid():N}@test.com");
        var (clientB, userB) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-b-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("User A Track", userId: userA);
        var request = MakePlaybackEvent("play_start", trackId);

        // Act - user B tries to report telemetry for user A's track
        var response = await clientB.PostAsJsonAsync("/telemetry/playback", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(403);
        problem.Type!.ShouldContain("track-access-denied");

        clientA.Dispose();
        clientB.Dispose();
    }

    // #13
    [Fact]
    public async Task IngestPlayback_Should_accept_all_valid_event_types()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("Multi Event Track", userId: userId);

        var eventTypes = new[] { "play_start", "play_stop", "play_progress", "play_complete", "seek" };

        // Act & Assert - all valid event types should be accepted
        foreach (var eventType in eventTypes)
        {
            var request = MakePlaybackEvent(eventType, trackId);
            var response = await client.PostAsJsonAsync("/telemetry/playback", request);

            response.StatusCode.ShouldBe(HttpStatusCode.Accepted,
                $"Expected 202 Accepted for event type '{eventType}' but got {(int)response.StatusCode}");

            var result = await response.Content.ReadFromJsonAsync<TelemetryAcceptedResponse>(JsonOptions);
            result.ShouldNotBeNull();
            result.Accepted.ShouldBeTrue();
        }

        client.Dispose();
    }

    // ========================================================================
    // 3.2 - Batch Ingestion
    // ========================================================================

    // #14
    [Fact]
    public async Task IngestBatch_Should_return_202_with_accepted_count()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("Batch Track", userId: userId);

        var events = new[]
        {
            MakePlaybackEvent("play_start", trackId),
            MakePlaybackEvent("play_progress", trackId, positionSeconds: 30),
            MakePlaybackEvent("play_complete", trackId, durationPlayedSeconds: 180)
        };
        var batchRequest = MakeBatchRequest(events);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback/batch", batchRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<TelemetryBatchResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Accepted.ShouldBe(3);
        result.Rejected.ShouldBe(0);
        result.CorrelationId.ShouldNotBeNullOrWhiteSpace();

        client.Dispose();
    }

    // #15
    [Fact]
    public async Task IngestBatch_Should_return_400_for_empty_batch()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var batchRequest = MakeBatchRequest(Array.Empty<object>());

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback/batch", batchRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(400);
        problem.Type!.ShouldContain("validation-error");

        client.Dispose();
    }

    // #16
    [Fact]
    public async Task IngestBatch_Should_return_400_for_batch_too_large()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = Ulid.NewUlid().ToString();

        // Create 51 events (exceeds MaxBatchSize of 50)
        var events = Enumerable.Range(0, 51)
            .Select(_ => MakePlaybackEvent("play_start", trackId))
            .ToArray();
        var batchRequest = MakeBatchRequest(events);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback/batch", batchRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(400);
        problem.Type!.ShouldContain("batch-too-large");

        client.Dispose();
    }

    // #17
    [Fact]
    public async Task IngestBatch_Should_return_400_for_invalid_event_type_in_batch()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = Ulid.NewUlid().ToString();

        var events = new[]
        {
            MakePlaybackEvent("play_start", trackId),
            MakePlaybackEvent("bad_event_type", trackId),
            MakePlaybackEvent("play_stop", trackId)
        };
        var batchRequest = MakeBatchRequest(events);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback/batch", batchRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(400);
        problem.Type!.ShouldContain("invalid-event-type");

        client.Dispose();
    }

    // #18
    [Fact]
    public async Task IngestBatch_Should_return_400_for_invalid_track_id_in_batch()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var validTrackId = Ulid.NewUlid().ToString();

        var events = new[]
        {
            MakePlaybackEvent("play_start", validTrackId),
            MakePlaybackEvent("play_start", "not-a-valid-ulid")
        };
        var batchRequest = MakeBatchRequest(events);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback/batch", batchRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(400);
        problem.Type!.ShouldContain("invalid-track-id");

        client.Dispose();
    }

    // #19
    [Fact]
    public async Task IngestBatch_Should_return_202_for_multiple_event_types()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        var trackId = await _factory.SeedTrackAsync("Mixed Events Track", userId: userId);

        var events = new[]
        {
            MakePlaybackEvent("play_start", trackId),
            MakePlaybackEvent("play_progress", trackId, positionSeconds: 60),
            MakePlaybackEvent("play_complete", trackId, durationPlayedSeconds: 200)
        };
        var batchRequest = MakeBatchRequest(events);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback/batch", batchRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<TelemetryBatchResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Accepted.ShouldBe(3);
        result.Rejected.ShouldBe(0);

        client.Dispose();
    }

    // #20
    [Fact]
    public async Task IngestBatch_Should_return_401_for_unauthenticated_requests()
    {
        // Arrange - use the shared unauthenticated client
        var client = _factory.Client;
        var trackId = Ulid.NewUlid().ToString();
        var events = new[] { MakePlaybackEvent("play_start", trackId) };
        var batchRequest = MakeBatchRequest(events);

        // Act
        var response = await client.PostAsJsonAsync("/telemetry/playback/batch", batchRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // #21
    [Fact]
    public async Task IngestBatch_Should_handle_partial_rejection()
    {
        // Arrange
        var (clientA, userA) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-a-{Guid.NewGuid():N}@test.com");
        var (clientB, userB) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-b-{Guid.NewGuid():N}@test.com");

        // Seed a track owned by user A
        var trackA = await _factory.SeedTrackAsync("User A Track", userId: userA);
        // Seed a track owned by user B
        var trackB = await _factory.SeedTrackAsync("User B Track", userId: userB);

        // User A submits a batch with one valid event (own track) and one that should be
        // rejected (user B's track - access denied)
        var events = new[]
        {
            MakePlaybackEvent("play_start", trackA),
            MakePlaybackEvent("play_start", trackB)
        };
        var batchRequest = MakeBatchRequest(events);

        // Act
        var response = await clientA.PostAsJsonAsync("/telemetry/playback/batch", batchRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var result = await response.Content.ReadFromJsonAsync<TelemetryBatchResponse>(JsonOptions);
        result.ShouldNotBeNull();
        result.Accepted.ShouldBe(1);
        result.Rejected.ShouldBeGreaterThan(0);
        result.CorrelationId.ShouldNotBeNullOrWhiteSpace();

        clientA.Dispose();
        clientB.Dispose();
    }
}
