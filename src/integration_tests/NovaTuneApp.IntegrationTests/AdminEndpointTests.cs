using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Admin;

namespace NovaTuneApp.Tests;

/// <summary>
/// Integration tests for the admin/moderation endpoints (Stage 8).
/// Tests user management, track moderation, analytics, and audit log endpoints.
/// Uses Aspire testing infrastructure with real containers.
/// </summary>
[Trait("Category", "Aspire")]
[Collection("Integration Tests")]
public class AdminEndpointTests : IAsyncLifetime
{
    private readonly IntegrationTestsApiFactory _factory;
    private readonly JsonSerializerOptions _jsonOptions;

    public AdminEndpointTests(IntegrationTestsApiFactory factory)
    {
        _factory = factory;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    public async Task InitializeAsync()
    {
        await _factory.ClearDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ========================================================================
    // Authorization Tests
    // ========================================================================

    [Fact]
    public async Task Admin_Endpoints_Should_return_401_for_unauthenticated_requests()
    {
        var client = _factory.CreateClient();

        // Try accessing admin endpoints without authentication
        var usersResponse = await client.GetAsync("/admin/users");
        usersResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var tracksResponse = await client.GetAsync("/admin/tracks");
        tracksResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var analyticsResponse = await client.GetAsync("/admin/analytics/overview");
        analyticsResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var auditResponse = await client.GetAsync("/admin/audit-logs");
        auditResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_Endpoints_Should_return_403_for_non_admin_users()
    {
        var (client, _) = await _factory.CreateAuthenticatedClientWithUserAsync("regularuser@example.com");

        // Try accessing admin endpoints as a regular user
        var usersResponse = await client.GetAsync("/admin/users");
        usersResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var tracksResponse = await client.GetAsync("/admin/tracks");
        tracksResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var analyticsResponse = await client.GetAsync("/admin/analytics/overview");
        analyticsResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ========================================================================
    // User Management Tests
    // ========================================================================

    [Fact]
    public async Task GET_admin_users_Should_return_200_with_user_list_for_admin()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin1@example.com");
        await _factory.SeedTestUsersAsync(5);

        var response = await client.GetAsync("/admin/users");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<AdminUserListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(5); // At least the seeded users + admin
    }

    [Fact]
    public async Task GET_admin_users_Should_support_search_filter()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin2@example.com");
        await _factory.SeedTestUsersAsync(5);

        var response = await client.GetAsync("/admin/users?search=testuser1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<AdminUserListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        // Should find users matching the search term
        result.Items.ShouldAllBe(u => u.Email.Contains("testuser1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GET_admin_users_Should_support_status_filter()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin3@example.com");
        await _factory.SeedTestUsersAsync(10); // Some will be disabled (every 5th)

        var response = await client.GetAsync("/admin/users?status=Disabled");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<AdminUserListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.ShouldAllBe(u => u.Status == UserStatus.Disabled);
    }

    [Fact]
    public async Task GET_admin_users_id_Should_return_user_details()
    {
        var (client, adminUserId) = await _factory.CreateAdminClientAsync("admin4@example.com");
        var userIds = await _factory.SeedTestUsersAsync(1);
        var testUserId = userIds[0];

        var response = await client.GetAsync($"/admin/users/{testUserId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var user = await response.Content.ReadFromJsonAsync<AdminUserDetails>(_jsonOptions);
        user.ShouldNotBeNull();
        user.UserId.ShouldBe(testUserId);
    }

    [Fact]
    public async Task GET_admin_users_id_Should_return_404_for_nonexistent_user()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin5@example.com");

        // Use a valid ULID that doesn't exist in the database
        var nonexistentUserId = Ulid.NewUlid().ToString();
        var response = await client.GetAsync($"/admin/users/{nonexistentUserId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PUT_admin_users_id_status_Should_update_user_status()
    {
        var (client, adminUserId) = await _factory.CreateAdminClientAsync("admin6@example.com");
        var userIds = await _factory.SeedTestUsersAsync(1);
        var testUserId = userIds[0];

        var request = new UpdateUserStatusRequest(
            UserStatus.Disabled,
            ModerationReasonCodes.CommunityGuidelines,
            "Test status change");

        var response = await client.PatchAsJsonAsync($"/admin/users/{testUserId}", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify the user status was updated
        var user = await _factory.GetUserByEmailAsync($"testuser0@example.com");
        user!.Status.ShouldBe(UserStatus.Disabled);

        // Verify audit log was created
        var auditCount = await _factory.GetAuditLogCountAsync();
        auditCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PUT_admin_users_id_status_Should_return_400_for_invalid_reason_code()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin7@example.com");
        var userIds = await _factory.SeedTestUsersAsync(1);
        var testUserId = userIds[0];

        var request = new UpdateUserStatusRequest(
            UserStatus.Disabled,
            "invalid_reason_code",
            "Test");

        var response = await client.PatchAsJsonAsync($"/admin/users/{testUserId}", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("invalid-reason-code");
    }

    [Fact]
    public async Task PUT_admin_users_id_status_Should_return_403_for_self_modification()
    {
        var (client, adminUserId) = await _factory.CreateAdminClientAsync("admin8@example.com");

        var request = new UpdateUserStatusRequest(
            UserStatus.Disabled,
            ModerationReasonCodes.Other,
            "Cannot disable self");

        var response = await client.PatchAsJsonAsync($"/admin/users/{adminUserId}", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problem.ShouldNotBeNull();
        problem.Type!.ShouldContain("self-modification");
    }

    // ========================================================================
    // Track Moderation Tests
    // ========================================================================

    [Fact]
    public async Task GET_admin_tracks_Should_return_200_with_track_list()
    {
        var (client, adminUserId) = await _factory.CreateAdminClientAsync("admin9@example.com");
        var userIds = await _factory.SeedTestUsersAsync(1);
        await _factory.SeedTrackAsync("Test Track 1", "Artist 1", userIds[0]);
        await _factory.SeedTrackAsync("Test Track 2", "Artist 2", userIds[0]);

        var response = await client.GetAsync("/admin/tracks");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<AdminTrackListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GET_admin_tracks_Should_support_moderation_status_filter()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin10@example.com");
        var userIds = await _factory.SeedTestUsersAsync(1);
        await _factory.SeedModeratedTrackAsync("Flagged Track", userIds[0], ModerationStatus.UnderReview);
        await _factory.SeedModeratedTrackAsync("Clean Track", userIds[0], ModerationStatus.None);

        var response = await client.GetAsync("/admin/tracks?moderationStatus=UnderReview");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<AdminTrackListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.ShouldAllBe(t => t.ModerationStatus == ModerationStatus.UnderReview);
    }

    [Fact]
    public async Task GET_admin_tracks_id_Should_return_track_details()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin11@example.com");
        var userIds = await _factory.SeedTestUsersAsync(1);
        var trackId = await _factory.SeedTrackAsync("Detail Track", "Detail Artist", userIds[0]);

        var response = await client.GetAsync($"/admin/tracks/{trackId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var track = await response.Content.ReadFromJsonAsync<AdminTrackDetails>(_jsonOptions);
        track.ShouldNotBeNull();
        track.TrackId.ShouldBe(trackId);
        track.Title.ShouldBe("Detail Track");
    }

    [Fact]
    public async Task PUT_admin_tracks_id_moderate_Should_update_moderation_status()
    {
        var (client, adminUserId) = await _factory.CreateAdminClientAsync("admin12@example.com");
        var userIds = await _factory.SeedTestUsersAsync(1);
        var trackId = await _factory.SeedTrackAsync("Track to Moderate", "Artist", userIds[0]);

        var request = new ModerateTrackRequest(
            ModerationStatus.Disabled,
            ModerationReasonCodes.CopyrightViolation,
            "Copyright claim received");

        var response = await client.PostAsJsonAsync($"/admin/tracks/{trackId}/moderate", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify the track moderation status was updated
        var track = await _factory.GetTrackByIdAsync(trackId);
        track!.ModerationStatus.ShouldBe(ModerationStatus.Disabled);
        track.ModerationReasonCode.ShouldBe(ModerationReasonCodes.CopyrightViolation);

        // Verify audit log was created
        var auditCount = await _factory.GetAuditLogCountAsync();
        auditCount.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PUT_admin_tracks_id_moderate_With_Removed_status_Should_trigger_soft_delete()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin13@example.com");
        var userIds = await _factory.SeedTestUsersAsync(1);
        var trackId = await _factory.SeedTrackAsync("Track to Remove", "Artist", userIds[0]);

        var request = new ModerateTrackRequest(
            ModerationStatus.Removed,
            ModerationReasonCodes.IllegalContent,
            "Illegal content detected");

        var response = await client.PostAsJsonAsync($"/admin/tracks/{trackId}/moderate", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify the track is soft-deleted
        var track = await _factory.GetTrackByIdAsync(trackId);
        track!.Status.ShouldBe(TrackStatus.Deleted);
        track.ScheduledDeletionAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task DELETE_admin_tracks_id_Should_soft_delete_track()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin14@example.com");
        var userIds = await _factory.SeedTestUsersAsync(1);
        var trackId = await _factory.SeedTrackAsync("Track to Delete", "Artist", userIds[0]);

        var request = new AdminDeleteTrackRequest(
            ModerationReasonCodes.CommunityGuidelines,
            "Admin deletion test");

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/admin/tracks/{trackId}")
        {
            Content = JsonContent.Create(request)
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify the track is soft-deleted
        var track = await _factory.GetTrackByIdAsync(trackId);
        track!.Status.ShouldBe(TrackStatus.Deleted);
        track.ModerationStatus.ShouldBe(ModerationStatus.Removed);
    }

    // ========================================================================
    // Analytics Tests
    // ========================================================================

    [Fact]
    public async Task GET_admin_analytics_overview_Should_return_analytics_data()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin15@example.com");
        await _factory.SeedTestUsersAsync(3);
        await _factory.SeedTestTracksAsync(5);

        var response = await client.GetAsync("/admin/analytics/overview");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var overview = await response.Content.ReadFromJsonAsync<AnalyticsOverview>(_jsonOptions);
        overview.ShouldNotBeNull();
        overview.TotalUsers.ShouldBeGreaterThanOrEqualTo(3);
        overview.TotalTracks.ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task GET_admin_analytics_top_tracks_Should_return_top_tracks()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin16@example.com");

        var response = await client.GetAsync("/admin/analytics/tracks/top?count=10&period=7d");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<TopTracksResponse>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Period.ShouldBe("7d");
    }

    [Fact]
    public async Task GET_admin_analytics_active_users_Should_return_active_users()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin17@example.com");

        var response = await client.GetAsync("/admin/analytics/users/active?count=10&period=30d");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ActiveUsersResponse>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Period.ShouldBe("30d");
    }

    // ========================================================================
    // Audit Log Tests
    // ========================================================================

    [Fact]
    public async Task GET_admin_audit_logs_Should_return_audit_entries()
    {
        var (client, adminUserId) = await _factory.CreateAdminClientAsync("admin18@example.com");

        // Perform an action that creates an audit log entry
        var userIds = await _factory.SeedTestUsersAsync(1);
        var request = new UpdateUserStatusRequest(
            UserStatus.Disabled,
            ModerationReasonCodes.Spam,
            "Spam account");

        await client.PatchAsJsonAsync($"/admin/users/{userIds[0]}", request);

        // Now fetch the audit logs
        var response = await client.GetAsync("/admin/audit-logs");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<AuditLogListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.Count.ShouldBeGreaterThanOrEqualTo(1);
        result.Items.ShouldContain(a => a.Action == AuditActions.UserStatusChanged);
    }

    [Fact]
    public async Task GET_admin_audit_logs_Should_support_action_filter()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin19@example.com");

        // Create some audit entries with different actions
        var userIds = await _factory.SeedTestUsersAsync(1);
        await client.PatchAsJsonAsync($"/admin/users/{userIds[0]}",
            new UpdateUserStatusRequest(UserStatus.Disabled, ModerationReasonCodes.Spam, "Test"));

        var trackId = await _factory.SeedTrackAsync("Track", "Artist", userIds[0]);
        await client.PostAsJsonAsync($"/admin/tracks/{trackId}/moderate",
            new ModerateTrackRequest(ModerationStatus.UnderReview, ModerationReasonCodes.Other, "Test"));

        // Filter by action
        var response = await client.GetAsync($"/admin/audit-logs?action={AuditActions.UserStatusChanged}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<AuditLogListItem>>(_jsonOptions);
        result.ShouldNotBeNull();
        result.Items.ShouldAllBe(a => a.Action == AuditActions.UserStatusChanged);
    }

    [Fact]
    public async Task GET_admin_audit_logs_id_Should_return_audit_details()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin20@example.com");

        // Create an audit entry
        var userIds = await _factory.SeedTestUsersAsync(1);
        await client.PatchAsJsonAsync($"/admin/users/{userIds[0]}",
            new UpdateUserStatusRequest(UserStatus.Disabled, ModerationReasonCodes.Other, "Test"));

        // Get the audit logs to find an ID
        var listResponse = await client.GetAsync("/admin/audit-logs");
        var list = await listResponse.Content.ReadFromJsonAsync<PagedResult<AuditLogListItem>>(_jsonOptions);
        var auditId = list!.Items.First().AuditId;

        // Get the details
        var response = await client.GetAsync($"/admin/audit-logs/{auditId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var details = await response.Content.ReadFromJsonAsync<AuditLogDetails>(_jsonOptions);
        details.ShouldNotBeNull();
        details.AuditId.ShouldBe(auditId);
        details.ReasonCode.ShouldBe(ModerationReasonCodes.Other);
    }

    [Fact]
    public async Task GET_admin_audit_logs_verify_Should_verify_hash_chain()
    {
        var (client, _) = await _factory.CreateAdminClientAsync("admin21@example.com");

        // Create some audit entries
        var userIds = await _factory.SeedTestUsersAsync(2);
        await client.PatchAsJsonAsync($"/admin/users/{userIds[0]}",
            new UpdateUserStatusRequest(UserStatus.Disabled, ModerationReasonCodes.Other, "Test 1"));
        await client.PatchAsJsonAsync($"/admin/users/{userIds[1]}",
            new UpdateUserStatusRequest(UserStatus.Disabled, ModerationReasonCodes.Spam, "Test 2"));

        // Verify integrity for today
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await client.GetAsync(
            $"/admin/audit-logs/verify?startDate={today:yyyy-MM-dd}&endDate={today:yyyy-MM-dd}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<AuditIntegrityResult>(_jsonOptions);
        result.ShouldNotBeNull();
        result.IsValid.ShouldBeTrue();
        result.EntriesChecked.ShouldBeGreaterThanOrEqualTo(2);
        result.InvalidEntries.ShouldBe(0);
    }
}
