# UI Tests Executable Implementation Plan (C# + Selenium + xUnit)

Execute phases sequentially (0 through 6). Each phase lists the exact files to create or modify, the code to write, and acceptance criteria. Do not proceed to the next phase until the current one passes its acceptance check.

## Task Summary

Implement UI tests in `src/ui_tests/NovaTuneApp.UiTests` using C#, Selenium WebDriver, and xUnit for the NovaTune player and admin Vue SPAs. The tests exercise real browser flows against an Aspire-hosted backend with Vite dev servers for the frontends.

## Current State (Verified)

- `src/ui_tests/` directory exists but is empty.
- Solution folder `ui_tests` (GUID `{5274C3EE-17E6-4E94-B417-2DA03947634D}`) exists in `NovaTuneApp.sln` with no project nested under it.
- Player `LoginPage.vue` has `data-testid="email"`, `data-testid="password"`, `data-testid="login-button"`.
- Player `RegisterPage.vue`, admin `AdminLoginPage.vue`, and admin `AdminRegisterPage.vue` have **no** `data-testid` attributes.
- `@playwright/test` is in player devDeps but no Playwright config or tests exist.
- Integration test factory (`IntegrationTestsApiFactory`) provides tested patterns for Aspire bootstrap, user registration, admin role granting, track/playlist seeding, and data clearing.

## Technology Stack

| Component | Choice | Version |
|---|---|---|
| Runtime | .NET 9.0 | `net9.0` |
| Test framework | xUnit | 2.9.3 |
| Browser automation | Selenium WebDriver | latest stable |
| Backend host | Aspire.Hosting.Testing | 13.0.0 |
| Assertions | Shouldly | 4.2.1 |
| Test SDK | Microsoft.NET.Test.Sdk | 17.14.1 |

---

## Phase 0: Add `data-testid` Attributes to Vue Components

Before writing any C# code, add stable test selectors to the auth pages, error states, and key layout elements.

### File: `src/NovaTuneClient/apps/player/src/features/auth/LoginPage.vue`

Already has `data-testid` on email, password, and login-button. Add `data-testid` to the error div (line 35):

```diff
-    <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm">
+    <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm" data-testid="error-message">
```

### File: `src/NovaTuneClient/apps/player/src/features/auth/RegisterPage.vue`

Add `data-testid` to each input, the submit button, and the error div:

```diff
     <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm">
+      data-testid="error-message"
```

Wait — the error div needs the attribute on the opening tag:

```diff
-    <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm">
+    <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm" data-testid="error-message">
```

```diff
      <input
        id="displayName"
        v-model="displayName"
        type="text"
        required
        class="input"
        placeholder="Your name"
+       data-testid="displayName"
      />
```

```diff
      <input
        id="email"
        v-model="email"
        type="email"
        required
        class="input"
        placeholder="you@example.com"
+       data-testid="email"
      />
```

```diff
      <input
        id="password"
        v-model="password"
        type="password"
        required
        minlength="8"
        class="input"
        placeholder="At least 8 characters"
+       data-testid="password"
      />
```

```diff
      <input
        id="confirmPassword"
        v-model="confirmPassword"
        type="password"
        required
        class="input"
        placeholder="Confirm your password"
+       data-testid="confirmPassword"
      />
```

```diff
-   <button
-     type="submit"
-     :disabled="isLoading"
-     class="w-full btn-primary disabled:opacity-50"
-   >
+   <button
+     type="submit"
+     :disabled="isLoading"
+     class="w-full btn-primary disabled:opacity-50"
+     data-testid="register-button"
+   >
```

### File: `src/NovaTuneClient/apps/admin/src/features/auth/AdminLoginPage.vue`

Add `data-testid` to email, password, submit button, error div, and success banner:

```diff
-    <div v-if="route.query.registered === 'true'" class="p-3 bg-green-900/50 border border-green-700 rounded-lg text-green-200 text-sm">
+    <div v-if="route.query.registered === 'true'" class="p-3 bg-green-900/50 border border-green-700 rounded-lg text-green-200 text-sm" data-testid="success-message">
```

```diff
-    <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm">
+    <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm" data-testid="error-message">
```

```diff
      <input
        id="email"
        v-model="email"
        type="email"
        required
        class="input"
        placeholder="admin@example.com"
+       data-testid="email"
      />
```

```diff
      <input
        id="password"
        v-model="password"
        type="password"
        required
        class="input"
        placeholder="Enter your password"
+       data-testid="password"
      />
```

```diff
-   <button
-     type="submit"
-     :disabled="isLoading"
-     class="w-full btn-primary disabled:opacity-50"
-   >
+   <button
+     type="submit"
+     :disabled="isLoading"
+     class="w-full btn-primary disabled:opacity-50"
+     data-testid="login-button"
+   >
```

### File: `src/NovaTuneClient/apps/admin/src/features/auth/AdminRegisterPage.vue`

Add `data-testid` to all inputs, submit button, and error div:

```diff
-    <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm">
+    <div v-if="error" class="p-3 bg-red-900/50 border border-red-700 rounded-lg text-red-200 text-sm" data-testid="error-message">
```

```diff
      <input
        id="displayName"
        v-model="displayName"
        type="text"
        required
        class="input"
        placeholder="Your name"
+       data-testid="displayName"
      />
```

```diff
      <input
        id="email"
        v-model="email"
        type="email"
        required
        class="input"
        placeholder="you@example.com"
+       data-testid="email"
      />
```

```diff
      <input
        id="password"
        v-model="password"
        type="password"
        required
        minlength="8"
        class="input"
        placeholder="At least 8 characters"
+       data-testid="password"
      />
```

```diff
      <input
        id="confirmPassword"
        v-model="confirmPassword"
        type="password"
        required
        class="input"
        placeholder="Confirm your password"
+       data-testid="confirmPassword"
      />
```

```diff
-   <button
-     type="submit"
-     :disabled="isLoading"
-     class="w-full btn-primary disabled:opacity-50"
-   >
+   <button
+     type="submit"
+     :disabled="isLoading"
+     class="w-full btn-primary disabled:opacity-50"
+     data-testid="register-button"
+   >
```

### File: `src/NovaTuneClient/apps/player/src/features/library/LibraryPage.vue`

Add `data-testid` to the heading, empty state, and track list container:

```diff
-     <h1 class="text-2xl font-bold text-white">My Library</h1>
+     <h1 class="text-2xl font-bold text-white" data-testid="library-heading">My Library</h1>
```

```diff
-   <div v-else-if="library.tracks.length === 0" class="card text-center py-12">
+   <div v-else-if="library.tracks.length === 0" class="card text-center py-12" data-testid="empty-state">
```

```diff
-   <div v-else class="space-y-2">
+   <div v-else class="space-y-2" data-testid="track-list">
```

### File: `src/NovaTuneClient/apps/player/src/features/playlists/PlaylistsPage.vue`

```diff
-     <h1 class="text-2xl font-bold text-white">Playlists</h1>
+     <h1 class="text-2xl font-bold text-white" data-testid="playlists-heading">Playlists</h1>
```

```diff
-     <button @click="showCreateModal = true" class="btn-primary flex items-center gap-2">
+     <button @click="showCreateModal = true" class="btn-primary flex items-center gap-2" data-testid="create-playlist-button">
```

```diff
-   <div v-else-if="playlists.playlists.length === 0" class="card text-center py-12">
+   <div v-else-if="playlists.playlists.length === 0" class="card text-center py-12" data-testid="empty-state">
```

Add to create-playlist modal inputs:

```diff
              <input
                id="name"
                v-model="newPlaylistName"
                type="text"
                required
                class="input"
                placeholder="My Playlist"
+               data-testid="playlist-name-input"
              />
```

```diff
-             <button type="submit" :disabled="isCreating" class="btn-primary">
+             <button type="submit" :disabled="isCreating" class="btn-primary" data-testid="playlist-submit-button">
```

### File: `src/NovaTuneClient/apps/admin/src/features/analytics/DashboardPage.vue`

```diff
-   <h1 class="text-2xl font-bold text-white mb-8">Dashboard</h1>
+   <h1 class="text-2xl font-bold text-white mb-8" data-testid="dashboard-heading">Dashboard</h1>
```

### Acceptance

`pnpm lint` and `pnpm typecheck` pass from `src/NovaTuneClient/`.

---

## Phase 1: Bootstrap UI Test Project

### File: `src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <RootNamespace>NovaTuneApp.UiTests</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Aspire.Hosting.Testing" Version="13.0.0" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
        <PackageReference Include="Selenium.WebDriver" Version="4.27.0" />
        <PackageReference Include="Selenium.Support" Version="4.27.0" />
        <PackageReference Include="Shouldly" Version="4.2.1" />
        <PackageReference Include="xunit" Version="2.9.3" />
        <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
    </ItemGroup>

    <ItemGroup>
        <Content Include="xunit.runner.json">
            <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
        </Content>
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\NovaTuneApp\NovaTuneApp.AppHost\NovaTuneApp.AppHost.csproj" />
        <ProjectReference Include="..\..\NovaTuneApp\NovaTuneApp.ApiService\NovaTuneApp.ApiService.csproj" />
    </ItemGroup>

    <ItemGroup>
        <Using Include="System.Net" />
        <Using Include="System.Net.Http.Json" />
        <Using Include="Aspire.Hosting.ApplicationModel" />
        <Using Include="Aspire.Hosting.Testing" />
        <Using Include="OpenQA.Selenium" />
        <Using Include="OpenQA.Selenium.Chrome" />
        <Using Include="OpenQA.Selenium.Support.UI" />
        <Using Include="Shouldly" />
        <Using Include="Xunit" />
    </ItemGroup>

</Project>
```

### File: `src/ui_tests/NovaTuneApp.UiTests/xunit.runner.json`

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false
}
```

### Add project to solution

```bash
cd /home/tassadar/Documents/GitHub/NovaTune

# Add the project to the solution under the existing ui_tests folder
dotnet sln src/NovaTuneApp/NovaTuneApp.sln add \
  src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj \
  --solution-folder ui_tests
```

After `dotnet sln add`, the `.sln` file will have a new project entry and a nesting entry mapping it under `{5274C3EE-17E6-4E94-B417-2DA03947634D}`.

### Acceptance

```bash
dotnet restore src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj
dotnet build src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj
dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj --list-tests
```

All three commands must succeed (no tests listed yet is fine).

---

## Phase 2: Test Host, WebDriver, and Fixtures

### File: `src/ui_tests/NovaTuneApp.UiTests/Fixtures/UiTestFixture.cs`

This is the shared collection fixture. It:
1. Starts the Aspire test host (Testing environment)
2. Starts player and admin Vite dev servers as child processes
3. Waits for all three endpoints to respond to HTTP
4. Provides base URLs, an API HttpClient, and a RavenDB DocumentStore for data seeding
5. Tears down Vite processes and Aspire host on dispose

```csharp
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Aspire.Hosting;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Models.Auth;
using NovaTuneApp.ApiService.Models.Identity;
using NovaTuneApp.ApiService.Models.Playlists;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Raven.Client.Documents.Operations;
using Serilog;

namespace NovaTuneApp.UiTests.Fixtures;

public class UiTestFixture : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private HttpClient _apiClient = null!;
    private IDocumentStore _documentStore = null!;
    private readonly List<Process> _viteProcesses = [];

    // URLs that Selenium tests navigate to
    public string PlayerBaseUrl { get; private set; } = null!;
    public string AdminBaseUrl { get; private set; } = null!;

    // API base URL for seeding via HTTP (register, login)
    public string ApiBaseUrl { get; private set; } = null!;
    public HttpClient ApiClient => _apiClient;

    public JsonSerializerOptions JsonOptions { get; } = new() { PropertyNameCaseInsensitive = true };

    // Fixed ports for Vite dev servers during tests (offset from dev ports 25173/25174)
    private const int PlayerPort = 26173;
    private const int AdminPort = 26174;

    public async Task InitializeAsync()
    {
        Log.CloseAndFlush();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Warning()
            .WriteTo.Console()
            .CreateLogger();

        // --- 1. Start Aspire backend ---
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.NovaTuneApp_AppHost>(
            [
                "--environment=Testing",
                "--NovaTune:CacheEncryption:Enabled=false",
                "--NovaTune:TopicPrefix=uitesting",
                "--Kafka:TopicPrefix=uitest",
                "--JWT_SIGNING_KEY=test-signing-key-must-be-at-least-32-characters-long-for-auth-tests",
                "--Jwt:Issuer=https://test.novatune.example",
                "--Jwt:Audience=novatune-test-api",
                "--Jwt:AccessTokenExpirationMinutes=15",
                "--Jwt:RefreshTokenExpirationMinutes=60",
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

        using var startupCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await _app.StartAsync(startupCts.Token);
        }
        catch (OperationCanceledException) when (startupCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Aspire app startup exceeded 3 minutes. Is a container runtime (Docker/Podman) running?");
        }

        using var healthCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("apiservice", healthCts.Token);

        _apiClient = _app.CreateHttpClient("apiservice");
        ApiBaseUrl = _apiClient.BaseAddress!.ToString().TrimEnd('/');

        // --- 2. Connect to RavenDB ---
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
        EnsureDatabaseExists(_documentStore, parts["Database"]);

        // --- 3. Start Vite dev servers ---
        var repoRoot = GetRepoRoot();
        var playerDir = Path.Combine(repoRoot, "src", "NovaTuneClient", "apps", "player");
        var adminDir = Path.Combine(repoRoot, "src", "NovaTuneClient", "apps", "admin");

        StartViteProcess(playerDir, PlayerPort, ApiBaseUrl);
        StartViteProcess(adminDir, AdminPort, ApiBaseUrl);

        PlayerBaseUrl = $"http://localhost:{PlayerPort}";
        AdminBaseUrl = $"http://localhost:{AdminPort}/admin/";

        // --- 4. Wait for dev servers to be ready ---
        await WaitForUrlAsync(PlayerBaseUrl, TimeSpan.FromSeconds(60));
        await WaitForUrlAsync(AdminBaseUrl, TimeSpan.FromSeconds(60));
    }

    public async Task DisposeAsync()
    {
        // Kill Vite processes
        foreach (var proc in _viteProcesses)
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    await proc.WaitForExitAsync();
                }
            }
            catch { /* best effort */ }
            finally
            {
                proc.Dispose();
            }
        }

        _documentStore?.Dispose();
        _apiClient?.Dispose();

        if (_app != null)
            await _app.DisposeAsync();
    }

    // ====================================================================
    // Data seeding helpers (mirror IntegrationTestsApiFactory patterns)
    // ====================================================================

    /// <summary>
    /// Clears all user, track, playlist, and audit data from the test database.
    /// Call at the start of each test class or test method for isolation.
    /// </summary>
    public async Task ClearDataAsync()
    {
        using var session = _documentStore.OpenAsyncSession();

        var users = await session.Query<ApplicationUser>()
            .Customize(x => x.WaitForNonStaleResults()).ToListAsync();
        foreach (var u in users) session.Delete(u);

        var tokens = await session.Query<RefreshToken>()
            .Customize(x => x.WaitForNonStaleResults()).ToListAsync();
        foreach (var t in tokens) session.Delete(t);

        var tracks = await session.Query<Track>()
            .Customize(x => x.WaitForNonStaleResults()).ToListAsync();
        foreach (var t in tracks) session.Delete(t);

        var playlists = await session.Query<Playlist>()
            .Customize(x => x.WaitForNonStaleResults()).ToListAsync();
        foreach (var p in playlists) session.Delete(p);

        var auditLogs = await session.Query<NovaTuneApp.ApiService.Models.Admin.AuditLogEntry>()
            .Customize(x => x.WaitForNonStaleResults()).ToListAsync();
        foreach (var log in auditLogs) session.Delete(log);

        session.Advanced.WaitForIndexesAfterSaveChanges();
        await session.SaveChangesAsync();
    }

    /// <summary>
    /// Registers a user via the API. Returns (email, password).
    /// Use the returned credentials in Selenium to log in through the UI.
    /// </summary>
    public async Task<(string Email, string Password)> SeedUserAsync(
        string email = "uitest@example.com",
        string displayName = "UI Test User",
        string password = "SecurePassword123!")
    {
        var request = new RegisterRequest(email, displayName, password);
        var response = await _apiClient.PostAsJsonAsync("/auth/register", request);
        response.EnsureSuccessStatusCode();
        return (email, password);
    }

    /// <summary>
    /// Registers a user and grants Admin role so the user can log into the admin app.
    /// </summary>
    public async Task<(string Email, string Password)> SeedAdminUserAsync(
        string email = "admin-uitest@example.com",
        string displayName = "Admin UI Test",
        string password = "SecurePassword123!")
    {
        await SeedUserAsync(email, displayName, password);

        using var session = _documentStore.OpenAsyncSession();
        var user = await session.Query<ApplicationUser>()
            .Customize(x => x.WaitForNonStaleResults())
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();

        user!.Roles = [.. user.Roles, "Admin"];
        user.Permissions = [.. user.Permissions, "audit.read"];
        session.Advanced.WaitForIndexesAfterSaveChanges();
        await session.SaveChangesAsync();

        return (email, password);
    }

    /// <summary>
    /// Seeds tracks owned by a specific user. Uses a single RavenDB session for efficiency.
    /// The userId must match the UserId field (ULID) of the ApplicationUser, NOT the email.
    /// </summary>
    public async Task<List<string>> SeedTracksForUserAsync(string userId, int count)
    {
        var trackIds = new List<string>();
        using var session = _documentStore.OpenAsyncSession();

        for (int i = 0; i < count; i++)
        {
            var trackId = Ulid.NewUlid().ToString();
            var now = DateTimeOffset.UtcNow;
            var track = new Track
            {
                Id = $"Tracks/{trackId}",
                TrackId = trackId,
                UserId = userId,
                Title = $"UI Test Track {i + 1}",
                Artist = $"UI Test Artist {i + 1}",
                Duration = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(i * 10),
                ObjectKey = $"audio/{trackId}.mp3",
                FileSizeBytes = 5_000_000,
                MimeType = "audio/mpeg",
                Status = TrackStatus.Ready,
                CreatedAt = now,
                UpdatedAt = now,
                ProcessedAt = now
            };

            await session.StoreAsync(track);
            trackIds.Add(trackId);
        }

        session.Advanced.WaitForIndexesAfterSaveChanges();
        await session.SaveChangesAsync();
        return trackIds;
    }

    /// <summary>
    /// Looks up a user by email and returns their ULID UserId.
    /// </summary>
    public async Task<string> GetUserIdByEmailAsync(string email)
    {
        using var session = _documentStore.OpenAsyncSession();
        var user = await session.Query<ApplicationUser>()
            .Customize(x => x.WaitForNonStaleResults())
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .FirstOrDefaultAsync();
        return user?.UserId ?? throw new InvalidOperationException($"User {email} not found");
    }

    // ====================================================================
    // Internal helpers
    // ====================================================================

    private void StartViteProcess(string workingDir, int port, string apiBaseUrl)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "pnpm",
            Arguments = "dev",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Override port and API URL for the test environment
        psi.Environment["PORT"] = port.ToString();
        psi.Environment["VITE_API_BASE_URL"] = apiBaseUrl;

        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start Vite in {workingDir}");

        _viteProcesses.Add(proc);
    }

    private static async Task WaitForUrlAsync(string url, TimeSpan timeout)
    {
        using var httpClient = new HttpClient();
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return; // Vite dev server is up (404 is fine — it means the server is responding)
            }
            catch (HttpRequestException)
            {
                // Not ready yet
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"URL {url} did not become available within {timeout.TotalSeconds}s");
    }

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

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "NovaTuneApp", "NovaTuneApp.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not find repository root (looked for src/NovaTuneApp/NovaTuneApp.sln above " +
            AppContext.BaseDirectory + ")");
    }
}
```

### File: `src/ui_tests/NovaTuneApp.UiTests/Fixtures/WebDriverFactory.cs`

Creates a ChromeDriver instance. Headless by default; set env var `UI_TESTS_HEADED=1` for local debugging.

```csharp
namespace NovaTuneApp.UiTests.Fixtures;

public static class WebDriverFactory
{
    public static ChromeDriver Create()
    {
        var options = new ChromeOptions();

        var headed = Environment.GetEnvironmentVariable("UI_TESTS_HEADED");
        if (headed != "1")
        {
            options.AddArgument("--headless=new");
        }

        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--window-size=1920,1080");

        // Selenium Manager handles chromedriver download automatically
        return new ChromeDriver(options);
    }
}
```

### File: `src/ui_tests/NovaTuneApp.UiTests/Fixtures/TestCollections.cs`

```csharp
namespace NovaTuneApp.UiTests.Fixtures;

[CollectionDefinition("UI Tests", DisableParallelization = true)]
public class UiTestCollection : ICollectionFixture<UiTestFixture>
{
}
```

### File: `src/ui_tests/NovaTuneApp.UiTests/Infrastructure/WaitHelpers.cs`

Centralized explicit-wait helpers. **No `Thread.Sleep` allowed anywhere in the test code.**

```csharp
namespace NovaTuneApp.UiTests.Infrastructure;

public static class WaitHelpers
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Waits until an element matching the given CSS selector is visible on the page.
    /// </summary>
    public static IWebElement WaitForElement(IWebDriver driver, By locator, TimeSpan? timeout = null)
    {
        var wait = new WebDriverWait(driver, timeout ?? DefaultTimeout);
        return wait.Until(d =>
        {
            var el = d.FindElement(locator);
            return el.Displayed ? el : null!;
        });
    }

    /// <summary>
    /// Waits until an element with the given data-testid is visible.
    /// </summary>
    public static IWebElement WaitForTestId(IWebDriver driver, string testId, TimeSpan? timeout = null)
    {
        return WaitForElement(driver, By.CssSelector($"[data-testid='{testId}']"), timeout);
    }

    /// <summary>
    /// Waits until the page URL contains the given substring.
    /// </summary>
    public static void WaitForUrlContains(IWebDriver driver, string urlPart, TimeSpan? timeout = null)
    {
        var wait = new WebDriverWait(driver, timeout ?? DefaultTimeout);
        wait.Until(d => d.Url.Contains(urlPart));
    }

    /// <summary>
    /// Waits until visible text matching the given string appears anywhere on the page.
    /// </summary>
    public static IWebElement WaitForText(IWebDriver driver, string text, TimeSpan? timeout = null)
    {
        var wait = new WebDriverWait(driver, timeout ?? DefaultTimeout);
        return wait.Until(d =>
        {
            var body = d.FindElement(By.TagName("body"));
            return body.Text.Contains(text) ? body : null!;
        });
    }

    /// <summary>
    /// Waits until visible text matching the given string appears anywhere on the page,
    /// then returns true. Returns false if the timeout expires.
    /// </summary>
    public static bool TextAppears(IWebDriver driver, string text, TimeSpan? timeout = null)
    {
        try
        {
            WaitForText(driver, text, timeout ?? TimeSpan.FromSeconds(5));
            return true;
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Clears localStorage to reset auth state, then navigates to the given URL.
    /// </summary>
    public static void ResetAndNavigate(IWebDriver driver, string url)
    {
        // Navigate to a blank page first if needed to have a valid origin for JS execution
        if (driver.Url == "data:," || driver.Url.StartsWith("about:"))
        {
            driver.Navigate().GoToUrl(url);
            // Clear storage once we have an origin
            ((IJavaScriptExecutor)driver).ExecuteScript("localStorage.clear(); sessionStorage.clear();");
            driver.Navigate().GoToUrl(url);
        }
        else
        {
            ((IJavaScriptExecutor)driver).ExecuteScript("localStorage.clear(); sessionStorage.clear();");
            driver.Navigate().GoToUrl(url);
        }
    }
}
```

### File: `src/ui_tests/NovaTuneApp.UiTests/Infrastructure/ScreenshotHelper.cs`

Captures a screenshot on test failure. Call from `DisposeAsync()`.

```csharp
namespace NovaTuneApp.UiTests.Infrastructure;

public static class ScreenshotHelper
{
    private static readonly string ScreenshotDir = Path.Combine(
        GetRepoRoot(), "src", "ui_tests", "NovaTuneApp.UiTests", "screenshots");

    /// <summary>
    /// Saves a timestamped screenshot. Safe to call even if the driver is null or disposed.
    /// </summary>
    public static void CaptureOnFailure(IWebDriver? driver, string testName)
    {
        if (driver is not ITakesScreenshot screenshotDriver)
            return;

        try
        {
            Directory.CreateDirectory(ScreenshotDir);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{testName}_{timestamp}.png";
            var filePath = Path.Combine(ScreenshotDir, fileName);

            var screenshot = screenshotDriver.GetScreenshot();
            screenshot.SaveAsFile(filePath);
        }
        catch
        {
            // Best effort — don't fail the test teardown
        }
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "NovaTuneApp", "NovaTuneApp.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return AppContext.BaseDirectory;
    }
}
```

### File: `src/ui_tests/NovaTuneApp.UiTests/Infrastructure/UiTestBase.cs`

Base class that handles driver lifecycle, login helper, and screenshot-on-failure.

```csharp
using NovaTuneApp.UiTests.Fixtures;

namespace NovaTuneApp.UiTests.Infrastructure;

/// <summary>
/// Base class for UI tests. Provides ChromeDriver lifecycle, login helpers,
/// and automatic screenshot capture on test failure.
/// </summary>
public abstract class UiTestBase : IAsyncLifetime
{
    protected readonly UiTestFixture Fixture;
    protected ChromeDriver Driver = null!;
    private bool _testPassed;

    protected UiTestBase(UiTestFixture fixture)
    {
        Fixture = fixture;
    }

    public virtual async Task InitializeAsync()
    {
        await Fixture.ClearDataAsync();
        Driver = WebDriverFactory.Create();
        _testPassed = false;
    }

    public virtual Task DisposeAsync()
    {
        if (!_testPassed)
        {
            ScreenshotHelper.CaptureOnFailure(Driver, GetType().Name);
        }

        Driver?.Quit();
        Driver?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Call at the end of a passing test to suppress failure screenshot.
    /// </summary>
    protected void MarkPassed() => _testPassed = true;

    /// <summary>
    /// Logs in to the player app through the UI.
    /// Navigates to /auth/login, fills credentials, clicks login, waits for "My Library".
    /// </summary>
    protected void PlayerLogin(string email, string password)
    {
        WaitHelpers.ResetAndNavigate(Driver, $"{Fixture.PlayerBaseUrl}/auth/login");

        WaitHelpers.WaitForTestId(Driver, "email").SendKeys(email);
        WaitHelpers.WaitForTestId(Driver, "password").SendKeys(password);
        WaitHelpers.WaitForTestId(Driver, "login-button").Click();

        WaitHelpers.WaitForText(Driver, "My Library");
    }

    /// <summary>
    /// Logs in to the admin app through the UI.
    /// Navigates to admin /auth/login, fills credentials, clicks login, waits for "Dashboard".
    /// </summary>
    protected void AdminLogin(string email, string password)
    {
        WaitHelpers.ResetAndNavigate(Driver, $"{Fixture.AdminBaseUrl}auth/login");

        WaitHelpers.WaitForTestId(Driver, "email").SendKeys(email);
        WaitHelpers.WaitForTestId(Driver, "password").SendKeys(password);
        WaitHelpers.WaitForTestId(Driver, "login-button").Click();

        WaitHelpers.WaitForText(Driver, "Dashboard");
    }
}
```

### Acceptance

The project should still build after adding these files:

```bash
dotnet build src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj
```

---

## Phase 3: Player UI Tests

### File: `src/ui_tests/NovaTuneApp.UiTests/Player/PlayerAuthTests.cs`

```csharp
using NovaTuneApp.UiTests.Fixtures;
using NovaTuneApp.UiTests.Infrastructure;

namespace NovaTuneApp.UiTests.Player;

[Trait("Category", "UI")]
[Trait("App", "Player")]
[Collection("UI Tests")]
public class PlayerAuthTests(UiTestFixture fixture) : UiTestBase(fixture)
{
    [Fact]
    public void Should_redirect_unauthenticated_user_to_login()
    {
        // Act — navigate to the root (requires auth)
        WaitHelpers.ResetAndNavigate(Driver, Fixture.PlayerBaseUrl);

        // Assert — should redirect to /auth/login
        WaitHelpers.WaitForUrlContains(Driver, "/auth/login");
        var heading = WaitHelpers.WaitForElement(Driver, By.TagName("h2"));
        heading.Text.ShouldBe("Sign In");
        MarkPassed();
    }

    [Fact]
    public async Task Should_login_successfully_with_valid_credentials()
    {
        // Arrange
        var (email, password) = await Fixture.SeedUserAsync();

        // Act — navigate to login page
        WaitHelpers.ResetAndNavigate(Driver, $"{Fixture.PlayerBaseUrl}/auth/login");

        var emailInput = WaitHelpers.WaitForTestId(Driver, "email");
        var passwordInput = WaitHelpers.WaitForTestId(Driver, "password");
        var loginButton = WaitHelpers.WaitForTestId(Driver, "login-button");

        emailInput.SendKeys(email);
        passwordInput.SendKeys(password);
        loginButton.Click();

        // Assert — should redirect to / and show "My Library"
        WaitHelpers.WaitForUrlContains(Driver, "/");
        WaitHelpers.WaitForText(Driver, "My Library");
        MarkPassed();
    }

    [Fact]
    public void Should_show_error_on_invalid_login()
    {
        // Act — navigate to login page with bad credentials
        WaitHelpers.ResetAndNavigate(Driver, $"{Fixture.PlayerBaseUrl}/auth/login");

        var emailInput = WaitHelpers.WaitForTestId(Driver, "email");
        var passwordInput = WaitHelpers.WaitForTestId(Driver, "password");
        var loginButton = WaitHelpers.WaitForTestId(Driver, "login-button");

        emailInput.SendKeys("nonexistent@example.com");
        passwordInput.SendKeys("WrongPassword123!");
        loginButton.Click();

        // Assert — stays on login page, shows error via data-testid
        var errorEl = WaitHelpers.WaitForTestId(Driver, "error-message");
        errorEl.Text.ShouldContain("Login failed");
        Driver.Url.ShouldContain("/auth/login");
        MarkPassed();
    }

    [Fact]
    public async Task Should_register_and_redirect_to_login_with_success_banner()
    {
        // Act — navigate to registration page
        WaitHelpers.ResetAndNavigate(Driver, $"{Fixture.PlayerBaseUrl}/auth/register");

        WaitHelpers.WaitForTestId(Driver, "displayName").SendKeys("New User");
        WaitHelpers.WaitForTestId(Driver, "email").SendKeys($"register-{Guid.NewGuid():N}@example.com");
        WaitHelpers.WaitForTestId(Driver, "password").SendKeys("SecurePassword123!");
        WaitHelpers.WaitForTestId(Driver, "confirmPassword").SendKeys("SecurePassword123!");
        WaitHelpers.WaitForTestId(Driver, "register-button").Click();

        // Assert — should redirect to login page (player register redirects to /)
        // The player RegisterPage.vue calls router.push('/') after successful registration
        WaitHelpers.WaitForText(Driver, "My Library");
        MarkPassed();
    }
}
```

### File: `src/ui_tests/NovaTuneApp.UiTests/Player/PlayerLibraryTests.cs`

```csharp
using NovaTuneApp.UiTests.Fixtures;
using NovaTuneApp.UiTests.Infrastructure;

namespace NovaTuneApp.UiTests.Player;

[Trait("Category", "UI")]
[Trait("App", "Player")]
[Collection("UI Tests")]
public class PlayerLibraryTests(UiTestFixture fixture) : UiTestBase(fixture)
{
    [Fact]
    public async Task Should_show_empty_state_for_new_user()
    {
        // Arrange — create user with no tracks
        var (email, password) = await Fixture.SeedUserAsync("library-empty@example.com");

        // Act — log in through the UI
        PlayerLogin(email, password);

        // Assert — Library page should show empty state
        WaitHelpers.WaitForTestId(Driver, "library-heading");
        WaitHelpers.WaitForText(Driver, "No tracks found");
        MarkPassed();
    }

    [Fact]
    public async Task Should_show_seeded_tracks_in_library()
    {
        // Arrange — create user and seed tracks
        var (email, password) = await Fixture.SeedUserAsync("library-test@example.com");
        var userId = await Fixture.GetUserIdByEmailAsync("library-test@example.com");
        await Fixture.SeedTracksForUserAsync(userId, 3);

        // Act — log in through the UI
        PlayerLogin(email, password);

        // Assert — Library page should show seeded tracks
        WaitHelpers.WaitForTestId(Driver, "track-list");
        WaitHelpers.WaitForText(Driver, "UI Test Track 1");
        WaitHelpers.WaitForText(Driver, "UI Test Track 2");
        WaitHelpers.WaitForText(Driver, "UI Test Track 3");
        MarkPassed();
    }
}
```

### File: `src/ui_tests/NovaTuneApp.UiTests/Player/PlayerPlaylistsTests.cs`

```csharp
using NovaTuneApp.UiTests.Fixtures;
using NovaTuneApp.UiTests.Infrastructure;

namespace NovaTuneApp.UiTests.Player;

[Trait("Category", "UI")]
[Trait("App", "Player")]
[Collection("UI Tests")]
public class PlayerPlaylistsTests(UiTestFixture fixture) : UiTestBase(fixture)
{
    [Fact]
    public async Task Should_show_empty_state_for_new_user()
    {
        // Arrange
        var (email, password) = await Fixture.SeedUserAsync("playlists-empty@example.com");

        // Act — log in and navigate to playlists
        PlayerLogin(email, password);
        Driver.Navigate().GoToUrl($"{Fixture.PlayerBaseUrl}/playlists");

        // Assert
        WaitHelpers.WaitForTestId(Driver, "playlists-heading");
        WaitHelpers.WaitForTestId(Driver, "empty-state");
        WaitHelpers.WaitForText(Driver, "No playlists yet");
        MarkPassed();
    }

    [Fact]
    public async Task Should_create_playlist_and_show_in_list()
    {
        // Arrange
        var (email, password) = await Fixture.SeedUserAsync("playlists-create@example.com");

        // Act — log in and navigate to playlists
        PlayerLogin(email, password);
        Driver.Navigate().GoToUrl($"{Fixture.PlayerBaseUrl}/playlists");
        WaitHelpers.WaitForTestId(Driver, "playlists-heading");

        // Click "New Playlist"
        var createButton = WaitHelpers.WaitForTestId(Driver, "create-playlist-button");
        createButton.Click();

        // Fill in the modal
        var nameInput = WaitHelpers.WaitForTestId(Driver, "playlist-name-input");
        nameInput.SendKeys("My First Playlist");

        var submitButton = WaitHelpers.WaitForTestId(Driver, "playlist-submit-button");
        submitButton.Click();

        // Assert — playlist should appear in the list
        WaitHelpers.WaitForText(Driver, "My First Playlist");
        MarkPassed();
    }
}
```

### Acceptance

```bash
dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj \
  --filter "FullyQualifiedName~Player" -c Debug
```

All Player tests pass in headless mode.

---

## Phase 4: Admin UI Tests

### File: `src/ui_tests/NovaTuneApp.UiTests/Admin/AdminAuthTests.cs`

```csharp
using NovaTuneApp.UiTests.Fixtures;
using NovaTuneApp.UiTests.Infrastructure;

namespace NovaTuneApp.UiTests.Admin;

[Trait("Category", "UI")]
[Trait("App", "Admin")]
[Collection("UI Tests")]
public class AdminAuthTests(UiTestFixture fixture) : UiTestBase(fixture)
{
    [Fact]
    public void Should_redirect_unauthenticated_user_to_admin_login()
    {
        // Act — navigate to admin root (requires auth + admin role)
        WaitHelpers.ResetAndNavigate(Driver, Fixture.AdminBaseUrl);

        // Assert — should redirect to /auth/login
        WaitHelpers.WaitForUrlContains(Driver, "/auth/login");
        var heading = WaitHelpers.WaitForElement(Driver, By.TagName("h2"));
        heading.Text.ShouldBe("Sign In");
        MarkPassed();
    }

    [Fact]
    public async Task Should_login_admin_successfully()
    {
        // Arrange — seed admin user
        var (email, password) = await Fixture.SeedAdminUserAsync();

        // Act
        WaitHelpers.ResetAndNavigate(Driver, $"{Fixture.AdminBaseUrl}auth/login");

        WaitHelpers.WaitForTestId(Driver, "email").SendKeys(email);
        WaitHelpers.WaitForTestId(Driver, "password").SendKeys(password);
        WaitHelpers.WaitForTestId(Driver, "login-button").Click();

        // Assert — should redirect to dashboard
        WaitHelpers.WaitForText(Driver, "Dashboard");
        WaitHelpers.WaitForText(Driver, "NovaTune Admin");
        MarkPassed();
    }

    [Fact]
    public async Task Should_reject_non_admin_login()
    {
        // Arrange — seed a regular (non-admin) user
        var (email, password) = await Fixture.SeedUserAsync("nonadmin@example.com");

        // Act
        WaitHelpers.ResetAndNavigate(Driver, $"{Fixture.AdminBaseUrl}auth/login");

        WaitHelpers.WaitForTestId(Driver, "email").SendKeys(email);
        WaitHelpers.WaitForTestId(Driver, "password").SendKeys(password);
        WaitHelpers.WaitForTestId(Driver, "login-button").Click();

        // Assert — should show error via data-testid and stay on login
        var errorEl = WaitHelpers.WaitForTestId(Driver, "error-message");
        errorEl.Text.ShouldContain("Admin access required");
        Driver.Url.ShouldContain("/auth/login");
        MarkPassed();
    }

    [Fact]
    public async Task Should_register_admin_and_redirect_to_login()
    {
        // Act — navigate to admin registration
        WaitHelpers.ResetAndNavigate(Driver, $"{Fixture.AdminBaseUrl}auth/register");

        WaitHelpers.WaitForTestId(Driver, "displayName").SendKeys("New Admin");
        WaitHelpers.WaitForTestId(Driver, "email").SendKeys($"admin-reg-{Guid.NewGuid():N}@example.com");
        WaitHelpers.WaitForTestId(Driver, "password").SendKeys("SecurePassword123!");
        WaitHelpers.WaitForTestId(Driver, "confirmPassword").SendKeys("SecurePassword123!");
        WaitHelpers.WaitForTestId(Driver, "register-button").Click();

        // Assert — admin RegisterPage redirects to login with ?registered=true
        WaitHelpers.WaitForUrlContains(Driver, "/auth/login");
        WaitHelpers.WaitForTestId(Driver, "success-message");
        WaitHelpers.WaitForText(Driver, "Account created successfully");
        MarkPassed();
    }
}
```

### File: `src/ui_tests/NovaTuneApp.UiTests/Admin/AdminDashboardTests.cs`

```csharp
using NovaTuneApp.UiTests.Fixtures;
using NovaTuneApp.UiTests.Infrastructure;

namespace NovaTuneApp.UiTests.Admin;

[Trait("Category", "UI")]
[Trait("App", "Admin")]
[Collection("UI Tests")]
public class AdminDashboardTests(UiTestFixture fixture) : UiTestBase(fixture)
{
    [Fact]
    public async Task Should_show_dashboard_with_stat_cards()
    {
        // Arrange
        var (email, password) = await Fixture.SeedAdminUserAsync();

        // Act — log in as admin
        AdminLogin(email, password);

        // Assert — dashboard heading and stat card labels should be visible
        WaitHelpers.WaitForTestId(Driver, "dashboard-heading");
        WaitHelpers.WaitForText(Driver, "Total Users");
        WaitHelpers.WaitForText(Driver, "Total Tracks");
        WaitHelpers.WaitForText(Driver, "Total Plays");
        WaitHelpers.WaitForText(Driver, "Active (24h)");
        MarkPassed();
    }

    [Fact]
    public async Task Should_show_sidebar_navigation()
    {
        // Arrange
        var (email, password) = await Fixture.SeedAdminUserAsync("sidebar-test@example.com");

        // Act
        AdminLogin(email, password);

        // Assert — sidebar nav items should be visible
        WaitHelpers.WaitForText(Driver, "Dashboard");
        WaitHelpers.WaitForText(Driver, "Users");
        WaitHelpers.WaitForText(Driver, "Tracks");
        WaitHelpers.WaitForText(Driver, "Analytics");
        WaitHelpers.WaitForText(Driver, "Audit Logs");
        MarkPassed();
    }
}
```

### Acceptance

```bash
dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj \
  --filter "FullyQualifiedName~Admin" -c Debug
```

All Admin tests pass in headless mode.

---

## Phase 5: Gitignore and Cleanup

### Append to `.gitignore`

```
# Selenium artifacts
src/ui_tests/**/screenshots/
src/ui_tests/**/logs/
```

---

## Phase 6: Verify Full Suite

Run the complete test suite and confirm all tests pass:

```bash
dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj -c Debug
```

---

## Test Scenario Summary

| # | Scope | Test | Selectors / Text Used |
|---|---|---|---|
| 1 | Player Auth | Redirect unauthenticated to login | URL `/auth/login`, `<h2>Sign In</h2>` |
| 2 | Player Auth | Login with valid credentials | `data-testid="email"`, `"password"`, `"login-button"`, text "My Library" |
| 3 | Player Auth | Invalid login shows error | `data-testid="error-message"`, text "Login failed" |
| 4 | Player Auth | Register and auto-login | `data-testid="displayName"`, `"email"`, `"password"`, `"confirmPassword"`, `"register-button"` |
| 5 | Player Library | Empty state for new user | `data-testid="library-heading"`, text "No tracks found" |
| 6 | Player Library | Shows seeded tracks | `data-testid="track-list"`, text "UI Test Track 1/2/3" |
| 7 | Player Playlists | Empty state for new user | `data-testid="playlists-heading"`, `"empty-state"`, text "No playlists yet" |
| 8 | Player Playlists | Create playlist flow | `data-testid="create-playlist-button"`, `"playlist-name-input"`, `"playlist-submit-button"` |
| 9 | Admin Auth | Redirect unauthenticated to login | URL `/auth/login`, `<h2>Sign In</h2>` |
| 10 | Admin Auth | Admin login succeeds | Text "Dashboard", "NovaTune Admin" |
| 11 | Admin Auth | Non-admin login rejected | `data-testid="error-message"`, text "Admin access required" |
| 12 | Admin Auth | Register and redirect to login | `data-testid="register-button"`, `"success-message"`, text "Account created" |
| 13 | Admin Dashboard | Shows stat cards | `data-testid="dashboard-heading"`, text "Total Users/Tracks/Plays", "Active (24h)" |
| 14 | Admin Dashboard | Shows sidebar nav | Text "Dashboard", "Users", "Tracks", "Analytics", "Audit Logs" |

---

## Dependency Graph

```
Phase 0 (data-testid attrs)
    |
Phase 1 (project bootstrap) <- depends on Phase 0
    |
Phase 2 (fixtures + helpers) <- depends on Phase 1
    |
    +-- Phase 3 (Player tests) <- depends on Phase 2
    |
    +-- Phase 4 (Admin tests)  <- depends on Phase 2
         |
Phase 5 (gitignore) <- depends on Phase 3 + Phase 4
    |
Phase 6 (full suite verification) <- depends on Phase 5
```

## Execution Order

1. Phase 0
2. Phase 1
3. Phase 2
4. Phase 3 + Phase 4 (can be written in parallel, must run sequentially due to shared collection fixture)
5. Phase 5
6. Phase 6

## Validation Commands

```bash
# Full suite
dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj -c Debug

# Player tests only
dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj -c Debug \
  --filter "Trait=App&Value=Player"

# Admin tests only
dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj -c Debug \
  --filter "Trait=App&Value=Admin"

# Headed mode for local debugging
UI_TESTS_HEADED=1 dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj -c Debug
```

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Flaky Selenium waits | Centralized `WaitHelpers` with explicit waits, no `Thread.Sleep` |
| Vite process leaks | `DisposeAsync` kills entire process tree, tracked in a list |
| Cross-test state leakage | `ClearDataAsync()` in base class `InitializeAsync`, fresh `ChromeDriver` per class, `localStorage.clear()` before each navigation |
| Chrome/Chromium path differs locally | Selenium Manager auto-resolves chromedriver; headless by default |
| Slow startup from Aspire + 2 Vite servers | Shared collection fixture starts everything once, all test classes reuse it |
| Test failure debugging | `ScreenshotHelper` captures PNG on failure, saved to `screenshots/` dir |
| Player auth store uses `VITE_API_BASE_URL` directly | Fixture sets `VITE_API_BASE_URL` env var when starting Vite, pointing to the test API |
| Admin uses `VITE_API_BASE_URL || '/api'` fallback | Same env var injection handles it |
| Port conflicts with real dev servers (25173/25174) | UI tests use offset ports (26173/26174) |

## Definition of Done

- [ ] All 4 auth pages + error divs have `data-testid` attributes
- [ ] `src/ui_tests/NovaTuneApp.UiTests` is a buildable .NET 9 xUnit project in the solution
- [ ] `UiTestFixture` starts Aspire backend + both Vite dev servers, provides seeding helpers
- [ ] `UiTestBase` provides driver lifecycle, login helpers, and screenshot-on-failure
- [ ] 14 test scenarios covering player auth, registration, library, playlists, and admin auth/dashboard
- [ ] `dotnet test src/ui_tests/NovaTuneApp.UiTests/NovaTuneApp.UiTests.csproj` passes in headless mode
- [ ] No `Thread.Sleep` in test code
- [ ] Selenium artifacts are gitignored
