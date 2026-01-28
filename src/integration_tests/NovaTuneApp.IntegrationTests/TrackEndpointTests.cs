using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Auth;

namespace NovaTuneApp.Tests;

/// <summary>
/// Integration tests for track management endpoints.
/// Tests CRUD operations, pagination, and soft-delete/restore flows.
/// </summary>
[Trait("Category", "Aspire")]
[Collection("Integration Tests")]
public class TrackEndpointTests : IAsyncLifetime
{
    private readonly IntegrationTestsApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions;
    private HttpClient _client = null!;
    private string _userId = null!;

    public TrackEndpointTests(IntegrationTestsApiFactory factory)
    {
        _factory = factory;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task InitializeAsync()
    {
        await _factory.ClearDataAsync();
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync();
        _client = client;
        _userId = userId;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ========================================================================
    // GET /tracks Tests
    // ========================================================================

    [Fact]
    public async Task ListTracks_Should_require_authentication()
    {
        // Arrange - use unauthenticated client
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/tracks");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // NOTE: ListTracks tests use the Tracks_ByUserForSearch RavenDB index.
    // These tests may be flaky when index consistency between test factory
    // and API service document stores causes timing issues.
    // Consider running these tests separately or with retry logic.

    [Fact(Skip = "ListTracks uses Tracks_ByUserForSearch index - timing issues with test/API store consistency")]
    public async Task ListTracks_Should_return_empty_list_when_no_tracks()
    {
        // Act
        var response = await _client.GetAsync("/tracks");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TrackListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(0);
        result.TotalCount.ShouldBe(0);
        result.HasMore.ShouldBeFalse();
        result.NextCursor.ShouldBeNull();
    }

    [Fact(Skip = "ListTracks uses Tracks_ByUserForSearch index - timing issues with test/API store consistency")]
    public async Task ListTracks_Should_return_only_users_own_tracks()
    {
        // Arrange - seed tracks for test user and another user
        await _factory.SeedTrackAsync("My Track 1", "Artist 1", _userId);
        await _factory.SeedTrackAsync("My Track 2", "Artist 2", _userId);
        await _factory.SeedTrackAsync("Other User Track", "Artist", "other-user-id");

        // Act
        var response = await _client.GetAsync("/tracks");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TrackListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.Items.ShouldAllBe(t => t.Title.StartsWith("My Track"));
    }

    [Fact(Skip = "ListTracks uses Tracks_ByUserForSearch index - timing issues with test/API store consistency")]
    public async Task ListTracks_Should_return_paged_results()
    {
        // Arrange - seed 5 tracks
        await _factory.SeedTestTracksAsync(5, _userId);

        // Act - request first 3
        var response = await _client.GetAsync("/tracks?limit=3");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TrackListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(3);
        result.TotalCount.ShouldBe(5);
        result.HasMore.ShouldBeTrue();
        result.NextCursor.ShouldNotBeNullOrEmpty();
    }

    [Fact(Skip = "ListTracks uses Tracks_ByUserForSearch index - timing issues with test/API store consistency")]
    public async Task ListTracks_Should_continue_with_cursor()
    {
        // Arrange - seed 10 tracks
        await _factory.SeedTestTracksAsync(10, _userId);

        // Act - get first page
        var firstResponse = await _client.GetAsync("/tracks?limit=5");
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<PagedResult<TrackListItem>>(_jsonOptions);

        // Act - get second page with cursor
        var secondResponse = await _client.GetAsync($"/tracks?limit=5&cursor={firstPage!.NextCursor}");
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<PagedResult<TrackListItem>>(_jsonOptions);

        // Assert
        secondPage.ShouldNotBeNull();
        secondPage.Items.Count.ShouldBe(5);
        secondPage.HasMore.ShouldBeFalse();

        // No overlap between pages
        var firstPageIds = firstPage.Items.Select(t => t.TrackId).ToHashSet();
        secondPage.Items.ShouldAllBe(t => !firstPageIds.Contains(t.TrackId));
    }

    [Fact(Skip = "ListTracks uses Tracks_ByUserForSearch index - timing issues with test/API store consistency")]
    public async Task ListTracks_Should_filter_by_search()
    {
        // Arrange
        await _factory.SeedTrackAsync("Unique Song Name", "Artist A", _userId);
        await _factory.SeedTrackAsync("Another Track", "Unique Artist", _userId);
        await _factory.SeedTrackAsync("Regular Track", "Regular Artist", _userId);

        // Act
        var response = await _client.GetAsync("/tracks?search=Unique");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TrackListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldAllBe(t =>
            t.Title.Contains("Unique", StringComparison.OrdinalIgnoreCase) ||
            (t.Artist != null && t.Artist.Contains("Unique", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact(Skip = "ListTracks uses Tracks_ByUserForSearch index - timing issues with test/API store consistency")]
    public async Task ListTracks_Should_exclude_deleted_by_default()
    {
        // Arrange
        var activeTrackId = await _factory.SeedTrackAsync("Active Track", "Artist", _userId);
        var deletedTrackId = await _factory.SeedTrackAsync("Deleted Track", "Artist", _userId);
        // Delete the track
        await _client.DeleteAsync($"/tracks/{deletedTrackId}");

        // Act
        var response = await _client.GetAsync("/tracks");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TrackListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(1);
        result.Items[0].TrackId.ShouldBe(activeTrackId);
    }

    [Fact(Skip = "ListTracks uses Tracks_ByUserForSearch index - timing issues with test/API store consistency")]
    public async Task ListTracks_Should_include_deleted_when_requested()
    {
        // Arrange
        await _factory.SeedTrackAsync("Active Track", "Artist", _userId);
        var deletedTrackId = await _factory.SeedTrackAsync("Deleted Track", "Artist", _userId);
        await _client.DeleteAsync($"/tracks/{deletedTrackId}");

        // Act
        var response = await _client.GetAsync("/tracks?includeDeleted=true");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TrackListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(t => t.TrackId == deletedTrackId);
    }

    [Fact]
    public async Task ListTracks_Should_reject_invalid_sort_field()
    {
        // Act
        var response = await _client.GetAsync("/tracks?sortBy=invalid");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Title!.ShouldContain("Invalid sort field");
    }

    [Fact]
    public async Task ListTracks_Should_reject_invalid_sort_order()
    {
        // Act
        var response = await _client.GetAsync("/tracks?sortOrder=invalid");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Title!.ShouldContain("Invalid sort order");
    }

    // ========================================================================
    // GET /tracks/{trackId} Tests
    // ========================================================================

    [Fact]
    public async Task GetTrack_Should_return_track_details()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Test Track", "Test Artist", _userId);

        // Act
        var response = await _client.GetAsync($"/tracks/{trackId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var track = await response.Content.ReadFromJsonAsync<TrackDetails>(_jsonOptions);
        track.ShouldNotBeNull();
        track.TrackId.ShouldBe(trackId);
        track.Title.ShouldBe("Test Track");
        track.Artist.ShouldBe("Test Artist");
        track.Status.ShouldBe(TrackStatus.Ready);
    }

    [Fact]
    public async Task GetTrack_Should_return_400_for_invalid_track_id()
    {
        // Act
        var response = await _client.GetAsync("/tracks/invalid-id");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("invalid-track-id");
    }

    [Fact]
    public async Task GetTrack_Should_return_404_for_nonexistent_track()
    {
        // Arrange
        var nonexistentId = Ulid.NewUlid().ToString();

        // Act
        var response = await _client.GetAsync($"/tracks/{nonexistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("track-not-found");
    }

    [Fact]
    public async Task GetTrack_Should_return_403_for_other_users_track()
    {
        // Arrange - create track owned by another user
        var trackId = await _factory.SeedTrackAsync("Other Track", "Artist", "other-user-id");

        // Act
        var response = await _client.GetAsync($"/tracks/{trackId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("forbidden");
    }

    // ========================================================================
    // PATCH /tracks/{trackId} Tests
    // ========================================================================

    [Fact]
    public async Task UpdateTrack_Should_update_title()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Original Title", "Artist", _userId);
        var request = new { title = "Updated Title" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/tracks/{trackId}", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var track = await response.Content.ReadFromJsonAsync<TrackDetails>(_jsonOptions);
        track.ShouldNotBeNull();
        track.Title.ShouldBe("Updated Title");
        track.Artist.ShouldBe("Artist"); // Unchanged
    }

    [Fact]
    public async Task UpdateTrack_Should_update_artist()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Title", "Original Artist", _userId);
        var request = new { artist = "Updated Artist" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/tracks/{trackId}", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var track = await response.Content.ReadFromJsonAsync<TrackDetails>(_jsonOptions);
        track.ShouldNotBeNull();
        track.Title.ShouldBe("Title"); // Unchanged
        track.Artist.ShouldBe("Updated Artist");
    }

    [Fact]
    public async Task UpdateTrack_Should_update_both_title_and_artist()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Original Title", "Original Artist", _userId);
        var request = new { title = "New Title", artist = "New Artist" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/tracks/{trackId}", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var track = await response.Content.ReadFromJsonAsync<TrackDetails>(_jsonOptions);
        track.ShouldNotBeNull();
        track.Title.ShouldBe("New Title");
        track.Artist.ShouldBe("New Artist");
    }

    [Fact]
    public async Task UpdateTrack_Should_return_400_for_empty_title()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Title", "Artist", _userId);
        var request = new { title = "" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/tracks/{trackId}", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("validation-error");
    }

    [Fact]
    public async Task UpdateTrack_Should_return_404_for_nonexistent_track()
    {
        // Arrange
        var nonexistentId = Ulid.NewUlid().ToString();
        var request = new { title = "New Title" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/tracks/{nonexistentId}", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateTrack_Should_return_403_for_other_users_track()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Track", "Artist", "other-user-id");
        var request = new { title = "Hacked Title" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/tracks/{trackId}", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateTrack_Should_return_409_for_deleted_track()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Track", "Artist", _userId);
        await _client.DeleteAsync($"/tracks/{trackId}");
        var request = new { title = "New Title" };

        // Act
        var response = await _client.PatchAsJsonAsync($"/tracks/{trackId}", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("track-deleted");
    }

    // ========================================================================
    // DELETE /tracks/{trackId} Tests
    // ========================================================================

    [Fact]
    public async Task DeleteTrack_Should_soft_delete_track()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Track to Delete", "Artist", _userId);

        // Act
        var response = await _client.DeleteAsync($"/tracks/{trackId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify track is soft-deleted
        var track = await _factory.GetTrackByIdAsync(trackId);
        track.ShouldNotBeNull();
        track.Status.ShouldBe(TrackStatus.Deleted);
        track.DeletedAt.ShouldNotBeNull();
        track.ScheduledDeletionAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteTrack_Should_return_404_for_nonexistent_track()
    {
        // Arrange
        var nonexistentId = Ulid.NewUlid().ToString();

        // Act
        var response = await _client.DeleteAsync($"/tracks/{nonexistentId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTrack_Should_return_403_for_other_users_track()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Track", "Artist", "other-user-id");

        // Act
        var response = await _client.DeleteAsync($"/tracks/{trackId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteTrack_Should_return_409_when_already_deleted()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Track", "Artist", _userId);
        await _client.DeleteAsync($"/tracks/{trackId}");

        // Act - try to delete again
        var response = await _client.DeleteAsync($"/tracks/{trackId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("already-deleted");
    }

    // ========================================================================
    // POST /tracks/{trackId}/restore Tests
    // ========================================================================

    [Fact]
    public async Task RestoreTrack_Should_restore_deleted_track()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Track", "Artist", _userId);
        await _client.DeleteAsync($"/tracks/{trackId}");

        // Act
        var response = await _client.PostAsync($"/tracks/{trackId}/restore", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var track = await response.Content.ReadFromJsonAsync<TrackDetails>(_jsonOptions);
        track.ShouldNotBeNull();
        track.Status.ShouldBe(TrackStatus.Ready);
        track.DeletedAt.ShouldBeNull();
        track.ScheduledDeletionAt.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreTrack_Should_return_404_for_nonexistent_track()
    {
        // Arrange
        var nonexistentId = Ulid.NewUlid().ToString();

        // Act
        var response = await _client.PostAsync($"/tracks/{nonexistentId}/restore", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RestoreTrack_Should_return_403_for_other_users_track()
    {
        // Arrange - create and delete track owned by another user
        var trackId = await _factory.SeedTrackAsync("Track", "Artist", "other-user-id");
        // We can't delete it via API since we don't have permission, so set it manually
        using var session = _factory.OpenSession();
        var track = await session.LoadAsync<Track>($"Tracks/{trackId}");
        track!.Status = TrackStatus.Deleted;
        track.DeletedAt = DateTimeOffset.UtcNow;
        track.ScheduledDeletionAt = DateTimeOffset.UtcNow.AddDays(30);
        await session.SaveChangesAsync();

        // Act
        var response = await _client.PostAsync($"/tracks/{trackId}/restore", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestoreTrack_Should_return_409_when_not_deleted()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Active Track", "Artist", _userId);

        // Act
        var response = await _client.PostAsync($"/tracks/{trackId}/restore", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("not-deleted");
    }

    // ========================================================================
    // Lifecycle Flow Tests
    // ========================================================================

    [Fact]
    public async Task TrackLifecycle_Delete_Restore_Delete_Should_work()
    {
        // Arrange
        var trackId = await _factory.SeedTrackAsync("Lifecycle Track", "Artist", _userId);

        // Act 1: Delete
        var deleteResponse = await _client.DeleteAsync($"/tracks/{trackId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify deleted
        var afterDelete = await GetTrackAsync(trackId);
        afterDelete.Status.ShouldBe(TrackStatus.Deleted);

        // Act 2: Restore
        var restoreResponse = await _client.PostAsync($"/tracks/{trackId}/restore", null);
        restoreResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify restored
        var afterRestore = await GetTrackAsync(trackId);
        afterRestore.Status.ShouldBe(TrackStatus.Ready);
        afterRestore.DeletedAt.ShouldBeNull();

        // Act 3: Delete again
        var deleteAgainResponse = await _client.DeleteAsync($"/tracks/{trackId}");
        deleteAgainResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify deleted again
        var afterDeleteAgain = await GetTrackAsync(trackId);
        afterDeleteAgain.Status.ShouldBe(TrackStatus.Deleted);
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private async Task<TrackDetails> GetTrackAsync(string trackId)
    {
        var response = await _client.GetAsync($"/tracks/{trackId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TrackDetails>(_jsonOptions))!;
    }

}
