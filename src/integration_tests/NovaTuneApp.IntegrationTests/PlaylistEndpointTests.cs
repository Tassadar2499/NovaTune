using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Playlists;

namespace NovaTuneApp.Tests;

/// <summary>
/// Integration tests for playlist endpoints.
/// Tests CRUD operations, track management, reorder, and lifecycle flows.
/// </summary>
[Collection("Integration Tests")]
[Trait("Category", "Aspire")]
public class PlaylistEndpointTests(IntegrationTestsApiFactory factory) : IAsyncLifetime
{
    private readonly IntegrationTestsApiFactory _factory = factory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync() => await _factory.ClearDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ========================================================================
    // 2.1 - CRUD Operations
    // ========================================================================

    // #1
    [Fact]
    public async Task Playlists_Should_return_401_for_unauthenticated_requests()
    {
        // Arrange - use the shared unauthenticated client
        var client = _factory.Client;

        // Act
        var getResponse = await client.GetAsync("/playlists");
        var postResponse = await client.PostAsJsonAsync("/playlists", new { Name = "Test" });

        // Assert
        getResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        postResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // #2
    [Fact]
    public async Task CreatePlaylist_Should_return_201_with_playlist_details()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var request = new { Name = "My Playlist", Description = "Test description" };

            // Act
            var response = await client.PostAsJsonAsync("/playlists", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.PlaylistId.ShouldNotBeNullOrWhiteSpace();
            playlist.Name.ShouldBe("My Playlist");
            playlist.Description.ShouldBe("Test description");
            playlist.TrackCount.ShouldBe(0);
            playlist.TotalDuration.ShouldBe(TimeSpan.Zero);
            playlist.Visibility.ShouldBe(PlaylistVisibility.Private);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #3
    [Fact]
    public async Task CreatePlaylist_Should_return_201_without_description()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var request = new { Name = "No Description Playlist" };

            // Act
            var response = await client.PostAsJsonAsync("/playlists", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.Name.ShouldBe("No Description Playlist");
            playlist.Description.ShouldBeNull();
        }
        finally
        {
            client.Dispose();
        }
    }

    // #4
    [Fact]
    public async Task CreatePlaylist_Should_return_400_for_empty_name()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var request = new { Name = "" };

            // Act
            var response = await client.PostAsJsonAsync("/playlists", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("validation-error");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #5
    [Fact]
    public async Task CreatePlaylist_Should_return_400_for_missing_name()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var request = new { Description = "No name" };

            // Act
            var response = await client.PostAsJsonAsync("/playlists", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #6
    [Fact]
    public async Task CreatePlaylist_Should_return_400_for_name_too_long()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var longName = new string('A', 101); // MaxNameLength=100
            var request = new { Name = longName };

            // Act
            var response = await client.PostAsJsonAsync("/playlists", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("validation-error");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #7
    [Fact]
    public async Task CreatePlaylist_Should_return_400_for_description_too_long()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var longDescription = new string('D', 501); // MaxDescriptionLength=500
            var request = new { Name = "Valid Name", Description = longDescription };

            // Act
            var response = await client.PostAsJsonAsync("/playlists", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("validation-error");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #8
    [Fact]
    public async Task CreatePlaylist_Should_return_403_when_quota_exceeded()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            // Seed 200 playlists (MaxPlaylistsPerUser) directly in DB
            for (int i = 0; i < 200; i++)
            {
                await _factory.SeedPlaylistAsync($"Playlist {i}", userId);
            }

            var request = new { Name = "Playlist 201" };

            // Act
            var response = await client.PostAsJsonAsync("/playlists", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(403);
            problem.Type!.ShouldContain("playlist-quota-exceeded");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #9
    [Fact]
    public async Task GetPlaylist_Should_return_playlist_with_tracks()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, trackIds) = await _factory.SeedPlaylistWithTracksAsync("With Tracks", userId, 3);

            // Act
            var response = await client.GetAsync($"/playlists/{playlistId}?includeTracks=true");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.PlaylistId.ShouldBe(playlistId);
            playlist.TrackCount.ShouldBe(3);
            playlist.Tracks.ShouldNotBeNull();
            playlist.Tracks.Items.Count.ShouldBe(3);

            var firstTrack = playlist.Tracks.Items[0];
            firstTrack.Position.ShouldBeGreaterThanOrEqualTo(0);
            firstTrack.TrackId.ShouldNotBeNullOrWhiteSpace();
            firstTrack.Title.ShouldNotBeNullOrWhiteSpace();
            firstTrack.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #10
    [Fact]
    public async Task GetPlaylist_Should_return_playlist_without_tracks()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, _) = await _factory.SeedPlaylistWithTracksAsync("Without Tracks View", userId, 2);

            // Act
            var response = await client.GetAsync($"/playlists/{playlistId}?includeTracks=false");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.PlaylistId.ShouldBe(playlistId);
            playlist.TrackCount.ShouldBe(2);
            playlist.Tracks.ShouldBeNull();
        }
        finally
        {
            client.Dispose();
        }
    }

    // #11
    [Fact]
    public async Task GetPlaylist_Should_support_track_pagination()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, _) = await _factory.SeedPlaylistWithTracksAsync("Paginated Tracks", userId, 5);

            // Act - request first page with limit of 2
            var response = await client.GetAsync(
                $"/playlists/{playlistId}?includeTracks=true&trackLimit=2");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.TrackCount.ShouldBe(5);
            playlist.Tracks.ShouldNotBeNull();
            playlist.Tracks.Items.Count.ShouldBe(2);
            playlist.Tracks.HasMore.ShouldBeTrue();
            playlist.Tracks.NextCursor.ShouldNotBeNullOrWhiteSpace();
        }
        finally
        {
            client.Dispose();
        }
    }

    // #12
    [Fact]
    public async Task GetPlaylist_Should_return_400_for_invalid_ulid()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            // Act
            var response = await client.GetAsync("/playlists/not-a-ulid");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("invalid-playlist-id");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #13
    [Fact]
    public async Task GetPlaylist_Should_return_404_for_nonexistent_playlist()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var fakeId = Ulid.NewUlid().ToString();

            // Act
            var response = await client.GetAsync($"/playlists/{fakeId}");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(404);
            problem.Type!.ShouldContain("playlist-not-found");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #14
    [Fact]
    public async Task GetPlaylist_Should_return_403_for_other_users_playlist()
    {
        // Arrange
        var (clientA, userA) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-a-{Guid.NewGuid():N}@test.com");
        var (clientB, userB) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-b-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("User A's Playlist", userA);

            // Act
            var response = await clientB.GetAsync($"/playlists/{playlistId}");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(403);
            problem.Type!.ShouldContain("forbidden");
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    // #15
    [Fact]
    public async Task UpdatePlaylist_Should_update_name()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Original Name", userId);
            var beforePlaylist = await _factory.GetPlaylistByIdAsync(playlistId);

            var request = new { Name = "Updated Name" };

            // Act
            var response = await client.PatchAsJsonAsync($"/playlists/{playlistId}", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.Name.ShouldBe("Updated Name");
            playlist.UpdatedAt.ShouldBeGreaterThanOrEqualTo(beforePlaylist!.UpdatedAt);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #16
    [Fact]
    public async Task UpdatePlaylist_Should_update_description()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Desc Test", userId, "Old description");
            var request = new { Description = "New description", HasDescription = true };

            // Act
            var response = await client.PatchAsJsonAsync($"/playlists/{playlistId}", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.Description.ShouldBe("New description");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #17
    [Fact]
    public async Task UpdatePlaylist_Should_clear_description()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Clear Desc", userId, "Will be cleared");
            var request = new { Description = (string?)null, HasDescription = true };

            // Act
            var response = await client.PatchAsJsonAsync($"/playlists/{playlistId}", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.Description.ShouldBeNull();
        }
        finally
        {
            client.Dispose();
        }
    }

    // #18
    [Fact]
    public async Task UpdatePlaylist_Should_return_400_for_empty_name()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Name Test", userId);
            var request = new { Name = "" };

            // Act
            var response = await client.PatchAsJsonAsync($"/playlists/{playlistId}", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("validation-error");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #19
    [Fact]
    public async Task UpdatePlaylist_Should_return_404_for_nonexistent_playlist()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var fakeId = Ulid.NewUlid().ToString();
            var request = new { Name = "Updated" };

            // Act
            var response = await client.PatchAsJsonAsync($"/playlists/{fakeId}", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(404);
            problem.Type!.ShouldContain("playlist-not-found");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #20
    [Fact]
    public async Task UpdatePlaylist_Should_return_403_for_other_users_playlist()
    {
        // Arrange
        var (clientA, userA) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-a-{Guid.NewGuid():N}@test.com");
        var (clientB, userB) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-b-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("User A's Playlist", userA);
            var request = new { Name = "Stolen" };

            // Act
            var response = await clientB.PatchAsJsonAsync($"/playlists/{playlistId}", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(403);
            problem.Type!.ShouldContain("forbidden");
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    // #21
    [Fact]
    public async Task DeletePlaylist_Should_return_204_and_remove_playlist()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("To Delete", userId);

            // Act
            var response = await client.DeleteAsync($"/playlists/{playlistId}");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            // Verify it's gone
            var getResponse = await client.GetAsync($"/playlists/{playlistId}");
            getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #22
    [Fact]
    public async Task DeletePlaylist_Should_return_404_for_nonexistent_playlist()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var fakeId = Ulid.NewUlid().ToString();

            // Act
            var response = await client.DeleteAsync($"/playlists/{fakeId}");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(404);
            problem.Type!.ShouldContain("playlist-not-found");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #23
    [Fact]
    public async Task DeletePlaylist_Should_return_403_for_other_users_playlist()
    {
        // Arrange
        var (clientA, userA) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-a-{Guid.NewGuid():N}@test.com");
        var (clientB, userB) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-b-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("User A's Playlist", userA);

            // Act
            var response = await clientB.DeleteAsync($"/playlists/{playlistId}");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(403);
            problem.Type!.ShouldContain("forbidden");
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    // #24
    [Fact]
    public async Task ListPlaylists_Should_return_users_playlists()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            await _factory.SeedPlaylistAsync("Playlist One", userId);
            await _factory.SeedPlaylistAsync("Playlist Two", userId);

            // Act - retry to handle RavenDB index timing
            PagedResult<PlaylistListItem>? result = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                var response = await client.GetAsync("/playlists");
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                result = await response.Content.ReadFromJsonAsync<PagedResult<PlaylistListItem>>(JsonOptions);
                if (result?.Items.Count >= 2 || attempt == 4) break;
                await Task.Delay(500);
            }

            // Assert
            result.ShouldNotBeNull();
            result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #25
    [Fact]
    public async Task ListPlaylists_Should_reject_invalid_sort_field()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            // Act
            var response = await client.GetAsync("/playlists?sortBy=invalid");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("invalid-query-parameter");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #26
    [Fact]
    public async Task ListPlaylists_Should_reject_invalid_sort_order()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            // Act
            var response = await client.GetAsync("/playlists?sortOrder=invalid");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("invalid-query-parameter");
        }
        finally
        {
            client.Dispose();
        }
    }

    // ========================================================================
    // 2.2 - Track Management
    // ========================================================================

    // #27
    [Fact]
    public async Task AddTracks_Should_add_single_track()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Add Single", userId);
            var trackId = await _factory.SeedTrackAsync("Track 1", "Artist 1", userId);
            var request = new { TrackIds = new[] { trackId } };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.TrackCount.ShouldBe(1);
            playlist.TotalDuration.ShouldBeGreaterThan(TimeSpan.Zero);

            // Verify track is in the playlist
            var getResponse = await client.GetAsync($"/playlists/{playlistId}?includeTracks=true");
            var details = await getResponse.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            details.ShouldNotBeNull();
            details.Tracks.ShouldNotBeNull();
            details.Tracks.Items.ShouldContain(t => t.TrackId == trackId);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #28
    [Fact]
    public async Task AddTracks_Should_add_multiple_tracks()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Add Multiple", userId);
            var trackId1 = await _factory.SeedTrackAsync("Track 1", "Artist 1", userId);
            var trackId2 = await _factory.SeedTrackAsync("Track 2", "Artist 2", userId);
            var trackId3 = await _factory.SeedTrackAsync("Track 3", "Artist 3", userId);
            var request = new { TrackIds = new[] { trackId1, trackId2, trackId3 } };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.TrackCount.ShouldBe(3);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #29
    [Fact]
    public async Task AddTracks_Should_append_to_end_by_default()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, existingTrackIds) = await _factory.SeedPlaylistWithTracksAsync(
                "Append Test", userId, 2);
            var newTrackId = await _factory.SeedTrackAsync("New Track", "New Artist", userId);
            var request = new { TrackIds = new[] { newTrackId } };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.TrackCount.ShouldBe(3);

            // Verify the new track is at the end
            var getResponse = await client.GetAsync($"/playlists/{playlistId}?includeTracks=true");
            var details = await getResponse.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            details.ShouldNotBeNull();
            details.Tracks.ShouldNotBeNull();
            var lastTrack = details.Tracks.Items.OrderByDescending(t => t.Position).First();
            lastTrack.TrackId.ShouldBe(newTrackId);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #30
    [Fact]
    public async Task AddTracks_Should_insert_at_position()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, existingTrackIds) = await _factory.SeedPlaylistWithTracksAsync(
                "Insert Test", userId, 2);
            var newTrackId = await _factory.SeedTrackAsync("Inserted Track", "Inserted Artist", userId);
            var request = new { TrackIds = new[] { newTrackId }, Position = 0 };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.TrackCount.ShouldBe(3);

            // Verify the new track is at position 0
            var getResponse = await client.GetAsync($"/playlists/{playlistId}?includeTracks=true");
            var details = await getResponse.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            details.ShouldNotBeNull();
            details.Tracks.ShouldNotBeNull();
            var firstTrack = details.Tracks.Items.OrderBy(t => t.Position).First();
            firstTrack.TrackId.ShouldBe(newTrackId);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #31
    [Fact]
    public async Task AddTracks_Should_return_400_for_empty_track_ids()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Empty TrackIds", userId);
            var request = new { TrackIds = Array.Empty<string>() };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("validation-error");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #32
    [Fact]
    public async Task AddTracks_Should_return_400_for_invalid_track_ulid()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Invalid ULID", userId);
            var request = new { TrackIds = new[] { "not-a-valid-ulid" } };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("invalid-track-id");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #33
    [Fact]
    public async Task AddTracks_Should_return_400_for_negative_position()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Negative Pos", userId);
            var trackId = await _factory.SeedTrackAsync("Track", "Artist", userId);
            var request = new { TrackIds = new[] { trackId }, Position = -1 };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("invalid-position");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #34
    [Fact]
    public async Task AddTracks_Should_return_400_for_too_many_tracks()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Too Many", userId);
            // Generate 101 fake but valid ULIDs (MaxTracksPerAddRequest=100)
            var trackIds = Enumerable.Range(0, 101)
                .Select(_ => Ulid.NewUlid().ToString())
                .ToArray();
            var request = new { TrackIds = trackIds };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("validation-error");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #35
    [Fact]
    public async Task AddTracks_Should_return_404_for_nonexistent_track()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Missing Track", userId);
            var fakeTrackId = Ulid.NewUlid().ToString();
            var request = new { TrackIds = new[] { fakeTrackId } };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(404);
            problem.Type!.ShouldContain("track-not-found");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #36
    [Fact]
    public async Task AddTracks_Should_return_409_for_deleted_track()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Deleted Track Test", userId);
            var trackId = await _factory.SeedTrackAsync("Deleted Track", "Artist", userId, TrackStatus.Deleted);
            var request = new { TrackIds = new[] { trackId } };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(409);
            problem.Type!.ShouldContain("track-deleted");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #37
    [Fact]
    public async Task AddTracks_Should_return_403_for_other_users_playlist()
    {
        // Arrange
        var (clientA, userA) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-a-{Guid.NewGuid():N}@test.com");
        var (clientB, userB) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-b-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("User A's Playlist", userA);
            var trackId = await _factory.SeedTrackAsync("Track", "Artist", userB);
            var request = new { TrackIds = new[] { trackId } };

            // Act
            var response = await clientB.PostAsJsonAsync($"/playlists/{playlistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(403);
            problem.Type!.ShouldContain("forbidden");
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    // #38
    [Fact]
    public async Task AddTracks_Should_return_404_for_nonexistent_playlist()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var fakePlaylistId = Ulid.NewUlid().ToString();
            var trackId = await _factory.SeedTrackAsync("Track", "Artist", userId);
            var request = new { TrackIds = new[] { trackId } };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{fakePlaylistId}/tracks", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(404);
            problem.Type!.ShouldContain("playlist-not-found");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #39
    [Fact]
    public async Task RemoveTrack_Should_return_204_and_decrement_count()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, trackIds) = await _factory.SeedPlaylistWithTracksAsync(
                "Remove Test", userId, 3);

            // Act - remove track at position 0
            var response = await client.DeleteAsync($"/playlists/{playlistId}/tracks/0");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            // Verify count is decremented
            var getResponse = await client.GetAsync($"/playlists/{playlistId}?includeTracks=false");
            var playlist = await getResponse.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.TrackCount.ShouldBe(2);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #40
    [Fact]
    public async Task RemoveTrack_Should_return_400_for_negative_position()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, _) = await _factory.SeedPlaylistWithTracksAsync("Neg Pos", userId, 1);

            // Act
            var response = await client.DeleteAsync($"/playlists/{playlistId}/tracks/-1");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("invalid-position");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #41
    [Fact]
    public async Task RemoveTrack_Should_return_404_for_position_out_of_range()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, _) = await _factory.SeedPlaylistWithTracksAsync("Out of Range", userId, 2);

            // Act - position 10 is out of range (only 2 tracks)
            var response = await client.DeleteAsync($"/playlists/{playlistId}/tracks/10");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(404);
            problem.Type!.ShouldContain("track-not-in-playlist");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #42
    [Fact]
    public async Task RemoveTrack_Should_return_403_for_other_users_playlist()
    {
        // Arrange
        var (clientA, userA) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-a-{Guid.NewGuid():N}@test.com");
        var (clientB, userB) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-b-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, _) = await _factory.SeedPlaylistWithTracksAsync(
                "User A's Playlist", userA, 1);

            // Act
            var response = await clientB.DeleteAsync($"/playlists/{playlistId}/tracks/0");

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(403);
            problem.Type!.ShouldContain("forbidden");
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    // ========================================================================
    // 2.3 - Reorder Tracks
    // ========================================================================

    // #43
    [Fact]
    public async Task ReorderTracks_Should_move_track_to_new_position()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, trackIds) = await _factory.SeedPlaylistWithTracksAsync(
                "Reorder Test", userId, 3);
            var request = new
            {
                Moves = new[] { new { From = 0, To = 2 } }
            };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/reorder", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.TrackCount.ShouldBe(3);

            // Verify positions changed
            var getResponse = await client.GetAsync($"/playlists/{playlistId}?includeTracks=true");
            var details = await getResponse.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            details.ShouldNotBeNull();
            details.Tracks.ShouldNotBeNull();
            details.Tracks.Items.Count.ShouldBe(3);

            // The first track (trackIds[0]) should now be at position 2
            var orderedTracks = details.Tracks.Items.OrderBy(t => t.Position).ToList();
            orderedTracks[2].TrackId.ShouldBe(trackIds[0]);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #44
    [Fact]
    public async Task ReorderTracks_Should_apply_multiple_moves()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, trackIds) = await _factory.SeedPlaylistWithTracksAsync(
                "Multi Move", userId, 4);
            var request = new
            {
                Moves = new[]
                {
                    new { From = 0, To = 3 },
                    new { From = 0, To = 2 }
                }
            };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/reorder", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await response.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.TrackCount.ShouldBe(4);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #45
    [Fact]
    public async Task ReorderTracks_Should_return_400_for_empty_moves()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, _) = await _factory.SeedPlaylistWithTracksAsync("Empty Moves", userId, 2);
            var request = new { Moves = Array.Empty<object>() };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/reorder", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("validation-error");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #46
    [Fact]
    public async Task ReorderTracks_Should_return_400_for_negative_from_position()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, _) = await _factory.SeedPlaylistWithTracksAsync("Neg From", userId, 2);
            var request = new
            {
                Moves = new[] { new { From = -1, To = 0 } }
            };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/reorder", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("invalid-position");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #47
    [Fact]
    public async Task ReorderTracks_Should_return_400_for_negative_to_position()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, _) = await _factory.SeedPlaylistWithTracksAsync("Neg To", userId, 2);
            var request = new
            {
                Moves = new[] { new { From = 0, To = -1 } }
            };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/reorder", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("invalid-position");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #48
    [Fact]
    public async Task ReorderTracks_Should_return_400_for_empty_playlist()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            var playlistId = await _factory.SeedPlaylistAsync("Empty Playlist", userId);
            var request = new
            {
                Moves = new[] { new { From = 0, To = 1 } }
            };

            // Act
            var response = await client.PostAsJsonAsync($"/playlists/{playlistId}/reorder", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(400);
            problem.Type!.ShouldContain("empty-playlist");
        }
        finally
        {
            client.Dispose();
        }
    }

    // #49
    [Fact]
    public async Task ReorderTracks_Should_return_403_for_other_users_playlist()
    {
        // Arrange
        var (clientA, userA) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-a-{Guid.NewGuid():N}@test.com");
        var (clientB, userB) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-b-{Guid.NewGuid():N}@test.com");
        try
        {
            var (playlistId, _) = await _factory.SeedPlaylistWithTracksAsync(
                "User A's Playlist", userA, 3);
            var request = new
            {
                Moves = new[] { new { From = 0, To = 2 } }
            };

            // Act
            var response = await clientB.PostAsJsonAsync($"/playlists/{playlistId}/reorder", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.ShouldNotBeNull();
            problem.Status.ShouldBe(403);
            problem.Type!.ShouldContain("forbidden");
        }
        finally
        {
            clientA.Dispose();
            clientB.Dispose();
        }
    }

    // ========================================================================
    // 2.4 - Lifecycle
    // ========================================================================

    // #50
    [Fact]
    public async Task PlaylistLifecycle_Create_AddTracks_Reorder_Remove_Delete()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            // Step 1: Create playlist
            var createRequest = new { Name = "Lifecycle Playlist", Description = "Full lifecycle test" };
            var createResponse = await client.PostAsJsonAsync("/playlists", createRequest);
            createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
            var created = await createResponse.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            created.ShouldNotBeNull();
            var playlistId = created.PlaylistId;
            created.TrackCount.ShouldBe(0);

            // Step 2: Add tracks
            var trackId1 = await _factory.SeedTrackAsync("Track 1", "Artist 1", userId);
            var trackId2 = await _factory.SeedTrackAsync("Track 2", "Artist 2", userId);
            var trackId3 = await _factory.SeedTrackAsync("Track 3", "Artist 3", userId);
            var addRequest = new { TrackIds = new[] { trackId1, trackId2, trackId3 } };
            var addResponse = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", addRequest);
            addResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var afterAdd = await addResponse.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            afterAdd.ShouldNotBeNull();
            afterAdd.TrackCount.ShouldBe(3);

            // Step 3: Reorder - move track from position 0 to position 2
            var reorderRequest = new
            {
                Moves = new[] { new { From = 0, To = 2 } }
            };
            var reorderResponse = await client.PostAsJsonAsync(
                $"/playlists/{playlistId}/reorder", reorderRequest);
            reorderResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var afterReorder = await reorderResponse.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            afterReorder.ShouldNotBeNull();
            afterReorder.TrackCount.ShouldBe(3);

            // Step 4: Remove a track
            var removeResponse = await client.DeleteAsync($"/playlists/{playlistId}/tracks/0");
            removeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            // Verify count decremented
            var getAfterRemove = await client.GetAsync($"/playlists/{playlistId}?includeTracks=false");
            var afterRemove = await getAfterRemove.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            afterRemove.ShouldNotBeNull();
            afterRemove.TrackCount.ShouldBe(2);

            // Step 5: Update playlist metadata
            var updateRequest = new { Name = "Updated Lifecycle Playlist" };
            var updateResponse = await client.PatchAsJsonAsync($"/playlists/{playlistId}", updateRequest);
            updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var afterUpdate = await updateResponse.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            afterUpdate.ShouldNotBeNull();
            afterUpdate.Name.ShouldBe("Updated Lifecycle Playlist");

            // Step 6: Delete playlist
            var deleteResponse = await client.DeleteAsync($"/playlists/{playlistId}");
            deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            // Verify deleted
            var getAfterDelete = await client.GetAsync($"/playlists/{playlistId}");
            getAfterDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
        finally
        {
            client.Dispose();
        }
    }

    // #51
    [Fact]
    public async Task PlaylistTrack_Should_reflect_deleted_track_status()
    {
        // Arrange
        var (client, userId) = await _factory.CreateAuthenticatedClientWithUserAsync(
            $"user-{Guid.NewGuid():N}@test.com");
        try
        {
            // Create a playlist with a track
            var playlistId = await _factory.SeedPlaylistAsync("Track Status Test", userId);
            var trackId = await _factory.SeedTrackAsync("Track To Delete", "Artist", userId);
            var addRequest = new { TrackIds = new[] { trackId } };
            var addResponse = await client.PostAsJsonAsync($"/playlists/{playlistId}/tracks", addRequest);
            addResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

            // Soft-delete the track via the tracks API (with rate limit retry — all tests share "anonymous" partition)
            HttpResponseMessage deleteResponse = null!;
            for (int i = 0; i < 4; i++)
            {
                deleteResponse = await client.DeleteAsync($"/tracks/{trackId}");
                if (deleteResponse.StatusCode != HttpStatusCode.TooManyRequests) break;
                await Task.Delay(11_000);
            }
            deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            // Act - get the playlist and check track status
            var getResponse = await client.GetAsync($"/playlists/{playlistId}?includeTracks=true");

            // Assert
            getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var playlist = await getResponse.Content.ReadFromJsonAsync<PlaylistDetails>(JsonOptions);
            playlist.ShouldNotBeNull();
            playlist.Tracks.ShouldNotBeNull();
            playlist.Tracks.Items.Count.ShouldBeGreaterThan(0);
            var track = playlist.Tracks.Items.First(t => t.TrackId == trackId);
            track.Status.ShouldBe(TrackStatus.Deleted);
        }
        finally
        {
            client.Dispose();
        }
    }
}
