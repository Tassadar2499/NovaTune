using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Microsoft.Extensions.Configuration;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Auth;
using NovaTuneApp.ApiService.Models.Identity;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Serilog;

namespace NovaTuneApp.Tests;

public class IntegrationTestsApiFactory : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private HttpClient _client = null!;
    private IDocumentStore _documentStore = null!;

    public HttpClient Client => _client;

    /// <summary>
    /// Creates a new HttpClient instance for tests that need their own client.
    /// </summary>
    public HttpClient CreateClient() => _app.CreateHttpClient("apiservice");

    public async Task InitializeAsync()
    {
        // Reset Serilog before creating the factory to avoid disposed provider references
        Log.CloseAndFlush();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        // Configure test-specific settings via command-line arguments
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.NovaTuneApp_AppHost>(
            [
                "--environment=Testing",
                // Testing runs should not require production secrets (e.g., cache encryption keys)
                "--NovaTune:CacheEncryption:Enabled=false",
                "--NovaTune:TopicPrefix=testing",
                "--Kafka:TopicPrefix=test",
                "--JWT_SIGNING_KEY=test-signing-key-must-be-at-least-32-characters-long-for-auth-tests",
                "--Jwt:Issuer=https://test.novatune.example",
                "--Jwt:Audience=novatune-test-api",
                "--Jwt:AccessTokenExpirationMinutes=15",
                "--Jwt:RefreshTokenExpirationMinutes=60",
                // High rate limits for tests
                "--RateLimiting:Auth:LoginPerIp:PermitLimit=1000",
                "--RateLimiting:Auth:LoginPerIp:WindowMinutes=1",
                "--RateLimiting:Auth:LoginPerAccount:PermitLimit=1000",
                "--RateLimiting:Auth:LoginPerAccount:WindowMinutes=1",
                "--RateLimiting:Auth:RegisterPerIp:PermitLimit=1000",
                "--RateLimiting:Auth:RegisterPerIp:WindowMinutes=1",
                "--RateLimiting:Auth:RefreshPerIp:PermitLimit=1000",
                "--RateLimiting:Auth:RefreshPerIp:WindowMinutes=1"
            ]);

        _app = await appHost.BuildAsync();

        // Start with timeout to fail fast instead of hanging indefinitely
        using var startupCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await _app.StartAsync(startupCts.Token);
        }
        catch (OperationCanceledException) when (startupCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Aspire app startup exceeded 3 minutes. Common causes: a container runtime (Docker/Podman) is not running, first-time image pulls are slow, or the API service is crash-looping due to missing configuration (e.g., NovaTune:CacheEncryption enabled without a key).");
        }

        // Wait for the API service to be healthy (not just running)
        // This ensures all health checks pass before tests run
        using var healthCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("apiservice", healthCts.Token);

        // Get the API service HTTP client
        _client = _app.CreateHttpClient("apiservice");

        // Get RavenDB connection for test utilities
        // Connection string format: "URL=http://localhost:8080;Database=novatune"
        var connectionString = await _app.GetConnectionStringAsync("novatune");
        var parts = connectionString!.Split(';')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => p[1]);

        _documentStore = new DocumentStore
        {
            Urls = [parts["URL"]],
            Database = parts["Database"]
        };
        _documentStore.Initialize();

        // Ensure the database exists (create if it doesn't)
        EnsureDatabaseExists(_documentStore, parts["Database"]);
    }

    /// <summary>
    /// Ensures the specified database exists, creating it if necessary.
    /// </summary>
    private static void EnsureDatabaseExists(IDocumentStore store, string databaseName)
    {
        try
        {
            store.Maintenance.ForDatabase(databaseName).Send(new GetStatisticsOperation());
        }
        catch (DatabaseDoesNotExistException)
        {
            store.Maintenance.Server.Send(new CreateDatabaseOperation(new DatabaseRecord(databaseName)));
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            _documentStore?.Dispose();
            _client?.Dispose();
        }
        finally
        {
            if (_app != null)
            {
                await _app.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Gets a user by email for test verification.
    /// </summary>
    public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
    {
        using var session = _documentStore.OpenAsyncSession();
        return await session.Query<ApplicationUser>()
            .Customize(x => x.WaitForNonStaleResults())
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Updates a user's status for test scenarios.
    /// </summary>
    public async Task UpdateUserStatusAsync(string email, UserStatus status)
    {
        using var session = _documentStore.OpenAsyncSession();
        var user = await session.Query<ApplicationUser>()
            .Customize(x => x.WaitForNonStaleResults())
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();

        if (user != null)
        {
            user.Status = status;
            // Ensure indexes are updated before returning so API sees the change
            session.Advanced.WaitForIndexesAfterSaveChanges();
            await session.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Gets the count of active tokens for a user.
    /// </summary>
    public async Task<int> GetActiveTokenCountAsync(string userId)
    {
        using var session = _documentStore.OpenAsyncSession();
        return await session.Query<RefreshToken>()
            .Customize(x => x.WaitForNonStaleResults())
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
        var users = await session.Query<ApplicationUser>()
            .Customize(x => x.WaitForNonStaleResults())
            .ToListAsync();
        foreach (var user in users)
            session.Delete(user);

        // Delete all RefreshTokens
        var tokens = await session.Query<RefreshToken>()
            .Customize(x => x.WaitForNonStaleResults())
            .ToListAsync();
        foreach (var token in tokens)
            session.Delete(token);

        // Delete all Tracks
        var tracks = await session.Query<Track>()
            .Customize(x => x.WaitForNonStaleResults())
            .ToListAsync();
        foreach (var track in tracks)
            session.Delete(track);

        // Delete all Audit Logs (for admin tests)
        var auditLogs = await session.Query<NovaTuneApp.ApiService.Models.Admin.AuditLogEntry>()
            .Customize(x => x.WaitForNonStaleResults())
            .ToListAsync();
        foreach (var log in auditLogs)
            session.Delete(log);

        // Wait for indexes to process the deletions before returning
        // This ensures subsequent test operations see a clean database
        session.Advanced.WaitForIndexesAfterSaveChanges();
        await session.SaveChangesAsync();
    }

    // ========================================================================
    // Track Test Helpers
    // ========================================================================

    /// <summary>
    /// Seeds a test track in the database.
    /// </summary>
    /// <param name="title">Track title.</param>
    /// <param name="artist">Track artist (optional).</param>
    /// <param name="userId">User ID who owns the track. If null, uses the test user ID.</param>
    /// <param name="status">Track status (default: Ready).</param>
    /// <returns>The track ID (ULID).</returns>
    public async Task<string> SeedTrackAsync(
        string title,
        string? artist = null,
        string? userId = null,
        TrackStatus status = TrackStatus.Ready)
    {
        var trackId = Ulid.NewUlid().ToString();
        var now = DateTimeOffset.UtcNow;

        var track = new Track
        {
            Id = $"Tracks/{trackId}",
            TrackId = trackId,
            UserId = userId ?? "test-user-id",
            Title = title,
            Artist = artist,
            Duration = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(Random.Shared.Next(0, 59)),
            ObjectKey = $"audio/{trackId}.mp3",
            FileSizeBytes = Random.Shared.Next(1_000_000, 10_000_000),
            MimeType = "audio/mpeg",
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
            ProcessedAt = status == TrackStatus.Ready ? now : null
        };

        using var session = _documentStore.OpenAsyncSession();
        await session.StoreAsync(track);
        session.Advanced.WaitForIndexesAfterSaveChanges();
        await session.SaveChangesAsync();

        return trackId;
    }

    /// <summary>
    /// Seeds multiple test tracks in the database.
    /// </summary>
    /// <param name="count">Number of tracks to create.</param>
    /// <param name="userId">User ID who owns the tracks.</param>
    /// <returns>List of track IDs (ULIDs).</returns>
    public async Task<List<string>> SeedTestTracksAsync(int count, string? userId = null)
    {
        var trackIds = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var trackId = await SeedTrackAsync($"Track {i + 1}", $"Artist {i + 1}", userId);
            trackIds.Add(trackId);
            // Small delay to ensure distinct CreatedAt timestamps for ordering tests
            await Task.Delay(10);
        }
        return trackIds;
    }

    /// <summary>
    /// Gets a track by ID for test verification.
    /// </summary>
    public async Task<Track?> GetTrackByIdAsync(string trackId)
    {
        using var session = _documentStore.OpenAsyncSession();
        return await session.LoadAsync<Track>($"Tracks/{trackId}");
    }

    /// <summary>
    /// Gets the count of tracks for a user.
    /// </summary>
    public async Task<int> GetTrackCountAsync(string? userId = null)
    {
        using var session = _documentStore.OpenAsyncSession();
        var query = session.Query<Track>()
            .Customize(x => x.WaitForNonStaleResults());

        if (userId != null)
            query = query.Where(t => t.UserId == userId);

        return await query.CountAsync();
    }

    /// <summary>
    /// Creates an authenticated HTTP client with a test user and returns it along with the user ID.
    /// </summary>
    public async Task<(HttpClient Client, string UserId)> CreateAuthenticatedClientWithUserAsync(
        string email = "testuser@example.com")
    {
        var client = CreateClient();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Register user
        var registerRequest = new RegisterRequest(email, "Test User", "SecurePassword123!");
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        // Login to get token
        var loginRequest = new LoginRequest(email, "SecurePassword123!");
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(jsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authResponse!.AccessToken);

        // Get the user ID from the database
        // Note: Use UserId (external ULID identifier), not Id (RavenDB document ID)
        var user = await GetUserByEmailAsync(email);

        return (client, user!.UserId);
    }

    /// <summary>
    /// Opens a RavenDB session for direct database access in tests.
    /// </summary>
    public Raven.Client.Documents.Session.IAsyncDocumentSession OpenSession()
    {
        return _documentStore.OpenAsyncSession();
    }

    // ========================================================================
    // Admin Test Helpers
    // ========================================================================

    /// <summary>
    /// Creates an authenticated HTTP client for an admin user.
    /// </summary>
    public async Task<(HttpClient Client, string UserId)> CreateAdminClientAsync(
        string email = "admin@example.com")
    {
        var client = CreateClient();
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Register user
        var registerRequest = new RegisterRequest(email, "Test User", "SecurePassword123!");
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        // Grant Admin role to the user BEFORE logging in so the JWT includes the role
        string userId;
        using (var session = _documentStore.OpenAsyncSession())
        {
            var dbUser = await session.Query<ApplicationUser>()
                .Customize(x => x.WaitForNonStaleResults())
                .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
                .FirstOrDefaultAsync();

            if (dbUser == null)
                throw new InvalidOperationException($"User with email {email} not found after registration");

            userId = dbUser.UserId;
            dbUser.Roles = [.. dbUser.Roles, "Admin"];
            dbUser.Permissions = [.. dbUser.Permissions, "audit.read"];
            await session.SaveChangesAsync();
        }

        // NOW login to get token with Admin role included
        var loginRequest = new LoginRequest(email, "SecurePassword123!");
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();

        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(jsonOptions);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authResponse!.AccessToken);

        return (client, userId);
    }

    /// <summary>
    /// Grants a role to an existing user.
    /// </summary>
    public async Task GrantRoleAsync(string userId, string role)
    {
        using var session = _documentStore.OpenAsyncSession();
        var user = await session.Query<ApplicationUser>()
            .Customize(x => x.WaitForNonStaleResults())
            .Where(u => u.UserId == userId)
            .FirstOrDefaultAsync();

        if (user != null && !user.Roles.Contains(role))
        {
            user.Roles = [.. user.Roles, role];
            session.Advanced.WaitForIndexesAfterSaveChanges();
            await session.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seeds multiple test users for admin testing.
    /// </summary>
    public async Task<List<string>> SeedTestUsersAsync(int count)
    {
        var userIds = new List<string>();
        var now = DateTime.UtcNow;

        using var session = _documentStore.OpenAsyncSession();

        for (int i = 0; i < count; i++)
        {
            var userId = Ulid.NewUlid().ToString();
            var user = new ApplicationUser
            {
                Id = $"ApplicationUsers/{userId}",
                UserId = userId,
                Email = $"testuser{i}@example.com",
                NormalizedEmail = $"TESTUSER{i}@EXAMPLE.COM",
                DisplayName = $"Test User {i}",
                Status = i % 5 == 0 ? UserStatus.Disabled : UserStatus.Active,
                Roles = [],
                TrackCount = Random.Shared.Next(0, 20),
                UsedStorageBytes = Random.Shared.Next(0, 100_000_000),
                CreatedAt = now.AddDays(-Random.Shared.Next(1, 100)),
                LastLoginAt = i % 3 == 0 ? now.AddHours(-Random.Shared.Next(1, 48)) : null
            };

            await session.StoreAsync(user);
            userIds.Add(userId);
        }

        session.Advanced.WaitForIndexesAfterSaveChanges();
        await session.SaveChangesAsync();

        return userIds;
    }

    /// <summary>
    /// Seeds a test track with moderation status for admin testing.
    /// </summary>
    public async Task<string> SeedModeratedTrackAsync(
        string title,
        string userId,
        NovaTuneApp.ApiService.Models.ModerationStatus moderationStatus,
        TrackStatus status = TrackStatus.Ready)
    {
        var trackId = Ulid.NewUlid().ToString();
        var now = DateTimeOffset.UtcNow;

        var track = new Track
        {
            Id = $"Tracks/{trackId}",
            TrackId = trackId,
            UserId = userId,
            Title = title,
            Artist = "Test Artist",
            Duration = TimeSpan.FromMinutes(3),
            ObjectKey = $"audio/{trackId}.mp3",
            FileSizeBytes = 5_000_000,
            MimeType = "audio/mpeg",
            Status = status,
            ModerationStatus = moderationStatus,
            CreatedAt = now,
            UpdatedAt = now,
            ProcessedAt = status == TrackStatus.Ready ? now : null
        };

        using var session = _documentStore.OpenAsyncSession();
        await session.StoreAsync(track);
        session.Advanced.WaitForIndexesAfterSaveChanges();
        await session.SaveChangesAsync();

        return trackId;
    }

    /// <summary>
    /// Gets the count of audit log entries.
    /// </summary>
    public async Task<int> GetAuditLogCountAsync()
    {
        using var session = _documentStore.OpenAsyncSession();
        return await session.Query<NovaTuneApp.ApiService.Models.Admin.AuditLogEntry>()
            .Customize(x => x.WaitForNonStaleResults())
            .CountAsync();
    }
}
