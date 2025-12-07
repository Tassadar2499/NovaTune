# Phase 8: Observability & Admin (FR 9.x, FR 11.x)

> **Status:** ⏳ Pending
> **Dependencies:** Phase 7 (all features complete)
> **Milestone:** M6 - Production

## Objective

Complete the platform with analytics dashboards, administrative controls, and production-grade observability.

---

## FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 9.1 | Upload Metrics | P2 | Test |
| FR 9.2 | Playback Metrics | P2 | Test |
| FR 9.3 | Admin Dashboards | P2 | Manual |
| FR 11.1 | System Visibility | P2 | Manual |
| FR 11.2 | Observability Access | P2 | Manual |
| FR 11.3 | User Moderation | P2 | Test |
| FR 11.4 | Configuration Management | P2 | Test |

## NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-4.1 | Metrics | OTEL exporters, dashboards |
| NF-4.2 | Logging | Correlation ID search |
| NF-4.3 | Alerting | PagerDuty/Slack integration |
| NF-4.4 | Distributed Tracing | W3C trace context |
| NF-5.1 | CI/CD Pipeline | Complete pipeline |
| NF-5.2 | Environments | Dev/staging/prod parity |

---

## Tasks

### Task 8.1: Analytics Event Consumers

**Priority:** P2 (Should-have)

Build Kafka consumers for analytics aggregation.

#### Subtasks

- [ ] **8.1.1** Create upload analytics consumer:
  ```csharp
  public class UploadAnalyticsConsumer : IKafkaConsumer<AudioUploadedEvent>
  {
      private readonly IAnalyticsService _analytics;
      private readonly ILogger<UploadAnalyticsConsumer> _logger;

      public async Task ConsumeAsync(
          AudioUploadedEvent @event,
          CancellationToken ct)
      {
          await _analytics.RecordUploadAsync(new UploadMetric
          {
              UserId = @event.UserId,
              TrackId = @event.TrackId,
              Format = @event.MimeType,
              SizeBytes = @event.FileSizeBytes,
              Timestamp = @event.Timestamp
          }, ct);

          _logger.LogInformation(
              "Recorded upload analytics. TrackId={TrackId}, Size={Size}",
              @event.TrackId, @event.FileSizeBytes);
      }
  }
  ```

- [ ] **8.1.2** Create playback analytics consumer:
  ```csharp
  public class PlaybackAnalyticsConsumer : IKafkaConsumer<PlaybackEvent>
  {
      public async Task ConsumeAsync(
          PlaybackEvent @event,
          CancellationToken ct)
      {
          await _analytics.RecordPlaybackAsync(new PlaybackMetric
          {
              UserId = @event.UserId,
              TrackId = @event.TrackId,
              Action = @event.Action,
              DurationMs = @event.DurationMs,
              Timestamp = @event.Timestamp
          }, ct);
      }
  }
  ```

- [ ] **8.1.3** Implement analytics service:
  ```csharp
  public interface IAnalyticsService
  {
      Task RecordUploadAsync(UploadMetric metric, CancellationToken ct);
      Task RecordPlaybackAsync(PlaybackMetric metric, CancellationToken ct);
      Task<UploadStats> GetUploadStatsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
      Task<PlaybackStats> GetPlaybackStatsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
      Task<UserStats> GetUserStatsAsync(string userId, CancellationToken ct);
  }
  ```

- [ ] **8.1.4** Store aggregations in RavenDB:
  ```csharp
  public sealed class DailyUploadStats
  {
      public string Id => $"Stats/Uploads/{Date:yyyy-MM-dd}";
      public DateOnly Date { get; init; }
      public int TotalUploads { get; set; }
      public long TotalBytes { get; set; }
      public TimeSpan TotalDuration { get; set; }
      public Dictionary<string, int> ByFormat { get; set; } = new();
      public int UniqueUsers { get; set; }
  }

  public sealed class DailyPlaybackStats
  {
      public string Id => $"Stats/Playback/{Date:yyyy-MM-dd}";
      public DateOnly Date { get; init; }
      public int TotalPlays { get; set; }
      public TimeSpan TotalListenTime { get; set; }
      public int UniqueListeners { get; set; }
      public Dictionary<string, int> TopTracks { get; set; } = new();
  }
  ```

- [ ] **8.1.5** Create aggregation index:
  ```csharp
  public class Stats_ByDate : AbstractMultiMapIndexCreationTask<StatsResult>
  {
      public Stats_ByDate()
      {
          AddMap<DailyUploadStats>(stats =>
              from stat in stats
              select new StatsResult
              {
                  Date = stat.Date,
                  TotalUploads = stat.TotalUploads,
                  TotalBytes = stat.TotalBytes,
                  TotalPlays = 0
              });

          AddMap<DailyPlaybackStats>(stats =>
              from stat in stats
              select new StatsResult
              {
                  Date = stat.Date,
                  TotalUploads = 0,
                  TotalBytes = 0,
                  TotalPlays = stat.TotalPlays
              });

          Reduce = results =>
              from result in results
              group result by result.Date into g
              select new StatsResult
              {
                  Date = g.Key,
                  TotalUploads = g.Sum(x => x.TotalUploads),
                  TotalBytes = g.Sum(x => x.TotalBytes),
                  TotalPlays = g.Sum(x => x.TotalPlays)
              };
      }
  }
  ```

- [ ] **8.1.6** Write consumer integration tests

#### Acceptance Criteria
- Events consumed and aggregated
- Daily stats updated accurately
- Historical queries work

---

### Task 8.2: Admin User Management

**Priority:** P2 (Should-have)

Implement administrative user management.

#### Subtasks

- [ ] **8.2.1** Create admin role:
  ```csharp
  public static class Roles
  {
      public const string Admin = "admin";
      public const string User = "user";
  }

  // Seed admin role
  public class AdminRoleSeeder : IHostedService
  {
      public async Task StartAsync(CancellationToken ct)
      {
          using var scope = _serviceProvider.CreateScope();
          var roleManager = scope.ServiceProvider
              .GetRequiredService<RoleManager<ApplicationRole>>();

          if (!await roleManager.RoleExistsAsync(Roles.Admin))
          {
              await roleManager.CreateAsync(new ApplicationRole
              {
                  Name = Roles.Admin,
                  NormalizedName = Roles.Admin.ToUpperInvariant()
              });
          }
      }
  }
  ```

- [ ] **8.2.2** Create `GET /api/v1/admin/users`:
  ```csharp
  app.MapGet("/api/v1/admin/users", async (
      [FromQuery] string? search,
      [FromQuery] UserStatus? status,
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 20,
      IAdminService adminService,
      CancellationToken ct) =>
  {
      var result = await adminService.GetUsersAsync(
          new UserSearchOptions
          {
              SearchTerm = search,
              Status = status,
              Page = page,
              PageSize = pageSize
          }, ct);

      return Results.Ok(result);
  })
  .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
  .WithName("AdminListUsers");
  ```

- [ ] **8.2.3** Create `POST /api/v1/admin/users/{id}/disable`:
  ```csharp
  app.MapPost("/api/v1/admin/users/{id}/disable", async (
      string id,
      DisableUserRequest request,
      IAdminService adminService,
      ClaimsPrincipal admin,
      CancellationToken ct) =>
  {
      var result = await adminService.DisableUserAsync(
          id, request.Reason, admin.GetUserId(), ct);

      return result.Match(
          _ => Results.NoContent(),
          error => Results.NotFound());
  })
  .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
  .WithName("AdminDisableUser");
  ```

- [ ] **8.2.4** Create `POST /api/v1/admin/users/{id}/enable`:
  ```csharp
  app.MapPost("/api/v1/admin/users/{id}/enable", async (
      string id,
      IAdminService adminService,
      ClaimsPrincipal admin,
      CancellationToken ct) =>
  {
      var result = await adminService.EnableUserAsync(
          id, admin.GetUserId(), ct);
      return result.Match(
          _ => Results.NoContent(),
          _ => Results.NotFound());
  })
  .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
  .WithName("AdminEnableUser");
  ```

- [ ] **8.2.5** Create `DELETE /api/v1/admin/users/{id}`:
  ```csharp
  app.MapDelete("/api/v1/admin/users/{id}", async (
      string id,
      IAdminService adminService,
      ClaimsPrincipal admin,
      CancellationToken ct) =>
  {
      // Hard delete (bypasses 30-day retention)
      var result = await adminService.DeleteUserAsync(
          id, admin.GetUserId(), ct);
      return result.Match(
          _ => Results.NoContent(),
          _ => Results.NotFound());
  })
  .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
  .WithName("AdminDeleteUser");
  ```

- [ ] **8.2.6** Implement admin service:
  ```csharp
  public sealed class AdminService : IAdminService
  {
      public async Task<Result<Unit, AdminError>> DisableUserAsync(
          string userId,
          string reason,
          string adminId,
          CancellationToken ct)
      {
          using var session = _store.OpenAsyncSession();

          var user = await session.LoadAsync<ApplicationUser>(userId, ct);
          if (user is null)
              return new NotFoundError();

          user.Status = UserStatus.Disabled;
          user.UpdatedAt = _timeProvider.GetUtcNow();

          // Record audit log
          await _auditLog.RecordAsync(new AuditEntry
          {
              Action = AuditAction.UserDisabled,
              TargetId = userId,
              PerformedBy = adminId,
              Reason = reason,
              Timestamp = _timeProvider.GetUtcNow()
          }, ct);

          // Revoke all sessions
          await _tokenService.RevokeAllUserTokensAsync(userId, ct);

          await session.SaveChangesAsync(ct);

          return Unit.Default;
      }
  }
  ```

- [ ] **8.2.7** Add audit logging

- [ ] **8.2.8** Write admin API tests

#### Acceptance Criteria
- Admin can search/filter users
- Disable/enable works with audit
- Hard delete removes all data
- Actions logged

---

### Task 8.3: Runtime Configuration Management

**Priority:** P2 (Should-have)

Allow runtime configuration changes without restart.

#### Subtasks

- [ ] **8.3.1** Define runtime configuration schema:
  ```csharp
  public sealed class RuntimeConfig
  {
      public int MaxFileSizeMb { get; set; } = 200;
      public string[] AllowedFormats { get; set; } =
          { "MP3", "WAV", "FLAC", "AAC", "OGG", "M4A" };
      public int MaxTracksPerUser { get; set; } = 1000;
      public int PresignedUrlTtlMin { get; set; } = 10;
      public bool EnableWaveformGeneration { get; set; } = false;
      public int MaxPlaylistsPerUser { get; set; } = 100;
      public int MaxTracksPerPlaylist { get; set; } = 500;
      public bool MaintenanceMode { get; set; } = false;
  }
  ```

- [ ] **8.3.2** Create `GET /api/v1/admin/config`:
  ```csharp
  app.MapGet("/api/v1/admin/config", async (
      IConfigurationService configService,
      CancellationToken ct) =>
  {
      var config = await configService.GetAsync(ct);
      return Results.Ok(config);
  })
  .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
  .WithName("GetConfig");
  ```

- [ ] **8.3.3** Create `PATCH /api/v1/admin/config`:
  ```csharp
  app.MapPatch("/api/v1/admin/config", async (
      UpdateConfigRequest request,
      IConfigurationService configService,
      ClaimsPrincipal admin,
      CancellationToken ct) =>
  {
      var result = await configService.UpdateAsync(
          request, admin.GetUserId(), ct);

      return result.Match(
          config => Results.Ok(config),
          error => Results.BadRequest(new { message = error.Message }));
  })
  .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
  .WithName("UpdateConfig");
  ```

- [ ] **8.3.4** Implement configuration service with RavenDB:
  ```csharp
  public sealed class ConfigurationService : IConfigurationService
  {
      private const string ConfigDocId = "Config/Runtime";

      public async Task<RuntimeConfig> GetAsync(CancellationToken ct)
      {
          using var session = _store.OpenAsyncSession();
          var config = await session.LoadAsync<RuntimeConfig>(ConfigDocId, ct);
          return config ?? new RuntimeConfig();
      }

      public async Task<Result<RuntimeConfig, ConfigError>> UpdateAsync(
          UpdateConfigRequest request,
          string adminId,
          CancellationToken ct)
      {
          using var session = _store.OpenAsyncSession();

          var config = await session.LoadAsync<RuntimeConfig>(ConfigDocId, ct)
              ?? new RuntimeConfig();

          // Validate ranges
          if (request.MaxFileSizeMb is { } maxSize)
          {
              if (maxSize < 10 || maxSize > 500)
                  return new ValidationError("MaxFileSizeMb must be 10-500");
              config.MaxFileSizeMb = maxSize;
          }

          // Apply other updates...

          await session.StoreAsync(config, ConfigDocId, ct);
          await session.SaveChangesAsync(ct);

          // Notify services to reload
          await _cache.RemoveAsync("config:runtime", ct);
          await _eventPublisher.PublishAsync(new ConfigUpdated(adminId));

          return config;
      }
  }
  ```

- [ ] **8.3.5** Implement hot reload:
  ```csharp
  public class RuntimeConfigOptions : IOptionsMonitor<RuntimeConfig>
  {
      private RuntimeConfig _current = new();
      private readonly IConfigurationService _configService;
      private readonly IMemoryCache _cache;

      public RuntimeConfig CurrentValue
      {
          get
          {
              return _cache.GetOrCreate("config:runtime", entry =>
              {
                  entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                  return _configService.GetAsync(CancellationToken.None)
                      .GetAwaiter().GetResult();
              })!;
          }
      }
  }
  ```

- [ ] **8.3.6** Validate configuration changes

- [ ] **8.3.7** Write configuration tests

#### Acceptance Criteria
- Config changes apply without restart
- Validation prevents invalid values
- Changes audited

---

### Task 8.4: Admin Dashboard API

**Priority:** P2 (Should-have)

Provide data for admin dashboard.

#### Subtasks

- [ ] **8.4.1** Create `GET /api/v1/admin/dashboard`:
  ```csharp
  app.MapGet("/api/v1/admin/dashboard", async (
      IAnalyticsService analytics,
      CancellationToken ct) =>
  {
      var now = _timeProvider.GetUtcNow();
      var stats = await analytics.GetDashboardStatsAsync(now, ct);
      return Results.Ok(stats);
  })
  .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
  .WithName("GetAdminDashboard");
  ```

- [ ] **8.4.2** Implement dashboard stats:
  ```csharp
  public record DashboardStats(
      UserStats Users,
      ContentStats Content,
      ActivityStats Activity,
      SystemStats System);

  public record UserStats(
      int TotalUsers,
      int ActiveLast7Days,
      int ActiveLast30Days,
      int NewLast7Days);

  public record ContentStats(
      int TotalTracks,
      long TotalStorageBytes,
      TimeSpan TotalDuration,
      Dictionary<string, int> TracksByFormat);

  public record ActivityStats(
      int UploadsToday,
      int PlaysToday,
      long BytesUploadedToday,
      Dictionary<string, double> ErrorRatesByEndpoint);

  public record SystemStats(
      double CpuUsage,
      double MemoryUsage,
      double DiskUsage,
      int ActiveConnections,
      double CacheHitRate);
  ```

- [ ] **8.4.3** Create `GET /api/v1/admin/users/top`:
  ```csharp
  app.MapGet("/api/v1/admin/users/top", async (
      [FromQuery] TopUsersCriteria criteria,
      [FromQuery] int limit = 10,
      IAnalyticsService analytics,
      CancellationToken ct) =>
  {
      var users = await analytics.GetTopUsersAsync(criteria, limit, ct);
      return Results.Ok(users);
  })
  .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
  .WithName("GetTopUsers");

  public enum TopUsersCriteria
  {
      Storage,
      Uploads,
      Plays
  }
  ```

- [ ] **8.4.4** Create system health endpoint

- [ ] **8.4.5** Write dashboard tests

#### Acceptance Criteria
- Dashboard shows current metrics
- Top users by various criteria
- System health visible

---

### Task 8.5: Alerting Integration

**Priority:** P2 (Should-have)

Set up alerting for operational issues.

#### Subtasks

- [ ] **8.5.1** Define alert rules:
  ```csharp
  public sealed class AlertRule
  {
      public string Id { get; init; } = string.Empty;
      public string Name { get; set; } = string.Empty;
      public AlertSeverity Severity { get; set; }
      public string Metric { get; set; } = string.Empty;
      public AlertCondition Condition { get; set; }
      public double Threshold { get; set; }
      public TimeSpan Window { get; set; }
      public TimeSpan CooldownPeriod { get; set; }
      public bool Enabled { get; set; } = true;
  }

  public enum AlertSeverity { P1Critical, P2High, P3Medium, P4Low }
  public enum AlertCondition { GreaterThan, LessThan, Equals }
  ```

- [ ] **8.5.2** Configure default alerts:
  ```csharp
  new AlertRule
  {
      Id = "error-rate-high",
      Name = "High Error Rate",
      Severity = AlertSeverity.P1Critical,
      Metric = "http_server_errors_total",
      Condition = AlertCondition.GreaterThan,
      Threshold = 0.05, // 5%
      Window = TimeSpan.FromMinutes(5),
      CooldownPeriod = TimeSpan.FromMinutes(15)
  },
  new AlertRule
  {
      Id = "latency-elevated",
      Name = "Elevated Latency",
      Severity = AlertSeverity.P2High,
      Metric = "http_request_duration_seconds_p95",
      Condition = AlertCondition.GreaterThan,
      Threshold = 2.0, // 2x normal
      Window = TimeSpan.FromMinutes(5),
      CooldownPeriod = TimeSpan.FromHours(1)
  },
  new AlertRule
  {
      Id = "storage-high",
      Name = "High Storage Usage",
      Severity = AlertSeverity.P3Medium,
      Metric = "storage_usage_percent",
      Condition = AlertCondition.GreaterThan,
      Threshold = 0.80, // 80%
      Window = TimeSpan.FromMinutes(15),
      CooldownPeriod = TimeSpan.FromHours(4)
  },
  new AlertRule
  {
      Id = "cache-hit-low",
      Name = "Low Cache Hit Rate",
      Severity = AlertSeverity.P4Low,
      Metric = "novatune_presigned_cache_hit_ratio",
      Condition = AlertCondition.LessThan,
      Threshold = 0.80, // 80%
      Window = TimeSpan.FromMinutes(30),
      CooldownPeriod = TimeSpan.FromDays(1)
  }
  ```

- [ ] **8.5.3** Implement alert evaluator:
  ```csharp
  public class AlertEvaluator : BackgroundService
  {
      protected override async Task ExecuteAsync(CancellationToken ct)
      {
          var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

          while (await timer.WaitForNextTickAsync(ct))
          {
              var rules = await _alertRules.GetEnabledAsync(ct);

              foreach (var rule in rules)
              {
                  try
                  {
                      await EvaluateRuleAsync(rule, ct);
                  }
                  catch (Exception ex)
                  {
                      _logger.LogError(ex, "Alert evaluation failed for {RuleId}",
                          rule.Id);
                  }
              }
          }
      }

      private async Task EvaluateRuleAsync(AlertRule rule, CancellationToken ct)
      {
          var value = await _metricsService.GetMetricValueAsync(
              rule.Metric, rule.Window, ct);

          var triggered = rule.Condition switch
          {
              AlertCondition.GreaterThan => value > rule.Threshold,
              AlertCondition.LessThan => value < rule.Threshold,
              AlertCondition.Equals => Math.Abs(value - rule.Threshold) < 0.001,
              _ => false
          };

          if (triggered && !await IsInCooldownAsync(rule.Id, ct))
          {
              await FireAlertAsync(rule, value, ct);
          }
      }
  }
  ```

- [ ] **8.5.4** Implement notification channels:
  ```csharp
  public interface IAlertChannel
  {
      Task SendAsync(Alert alert, CancellationToken ct);
  }

  public class SlackAlertChannel : IAlertChannel
  {
      public async Task SendAsync(Alert alert, CancellationToken ct)
      {
          var payload = new
          {
              channel = GetChannel(alert.Severity),
              text = $":alert: *{alert.Severity}* - {alert.RuleName}",
              attachments = new[]
              {
                  new
                  {
                      color = GetColor(alert.Severity),
                      fields = new[]
                      {
                          new { title = "Metric", value = alert.Metric, @short = true },
                          new { title = "Value", value = $"{alert.Value:F2}", @short = true },
                          new { title = "Threshold", value = $"{alert.Threshold:F2}", @short = true }
                      }
                  }
              }
          };

          await _httpClient.PostAsJsonAsync(_webhookUrl, payload, ct);
      }
  }
  ```

- [ ] **8.5.5** Create PagerDuty integration

- [ ] **8.5.6** Add alert history and acknowledgment

- [ ] **8.5.7** Write alerting tests

#### Acceptance Criteria
- Alerts fire on threshold breach
- Cooldown prevents spam
- Multiple notification channels
- Alert history maintained

---

### Task 8.6: Structured Logging Enhancement

**Priority:** P2 (Should-have)

Enhance logging with correlation ID search and requirement tracing.

#### Subtasks

- [ ] **8.6.1** Add correlation ID propagation:
  ```csharp
  public class CorrelationIdMiddleware
  {
      public async Task InvokeAsync(HttpContext context, RequestDelegate next)
      {
          var correlationId = context.Request.Headers["X-Correlation-ID"]
              .FirstOrDefault() ?? Guid.NewGuid().ToString();

          context.Items["CorrelationId"] = correlationId;
          context.Response.Headers["X-Correlation-ID"] = correlationId;

          using (LogContext.PushProperty("CorrelationId", correlationId))
          {
              await next(context);
          }
      }
  }
  ```

- [ ] **8.6.2** Add requirement ID logging:
  ```csharp
  [AttributeUsage(AttributeTargets.Method)]
  public class RequirementAttribute : Attribute
  {
      public string Id { get; }
      public RequirementAttribute(string id) => Id = id;
  }

  // Usage
  [Requirement("FR-2.3")]
  app.MapPost("/api/v1/tracks/upload", ...);

  // Middleware to extract and log
  public class RequirementLoggingMiddleware
  {
      public async Task InvokeAsync(HttpContext context, RequestDelegate next)
      {
          var endpoint = context.GetEndpoint();
          var reqAttr = endpoint?.Metadata.GetMetadata<RequirementAttribute>();

          if (reqAttr is not null)
          {
              using (LogContext.PushProperty("req", reqAttr.Id))
              {
                  await next(context);
              }
          }
          else
          {
              await next(context);
          }
      }
  }
  ```

- [ ] **8.6.3** Implement log search API:
  ```csharp
  app.MapGet("/api/v1/admin/logs/search", async (
      [FromQuery] string correlationId,
      [FromQuery] DateTimeOffset? from,
      [FromQuery] DateTimeOffset? to,
      ILogSearchService logSearch,
      CancellationToken ct) =>
  {
      var logs = await logSearch.SearchByCorrelationIdAsync(
          correlationId, from, to, ct);
      return Results.Ok(logs);
  })
  .RequireAuthorization(policy => policy.RequireRole(Roles.Admin))
  .WithName("SearchLogs");
  ```

- [ ] **8.6.4** Hash user IDs in non-debug logs:
  ```csharp
  public static class LogExtensions
  {
      public static string Hash(this string userId)
      {
          using var sha = SHA256.Create();
          var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(userId));
          return Convert.ToHexString(hash)[..16]; // First 16 chars
      }
  }
  ```

- [ ] **8.6.5** Write logging tests

#### Acceptance Criteria
- All logs include correlation ID
- Requirement IDs in relevant logs
- User IDs hashed in production
- Log search by correlation ID works

---

### Task 8.7: CI/CD Pipeline Completion

**Priority:** P2 (Should-have)

Finalize production CI/CD pipeline.

#### Subtasks

- [ ] **8.7.1** Add security scanning:
  ```yaml
  security-scan:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      # Secret scanning
      - name: Run gitleaks
        uses: gitleaks/gitleaks-action@v2

      # SAST
      - name: Run Semgrep
        uses: semgrep/semgrep-action@v1
        with:
          config: p/security-audit

      # Dependency scanning
      - name: Run Snyk
        uses: snyk/actions/dotnet@master
        with:
          args: --severity-threshold=high
  ```

- [ ] **8.7.2** Add OpenAPI schema diff:
  ```yaml
  openapi-diff:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Generate current schema
        run: dotnet run --project src/NovaTuneApp/NovaTuneApp.ApiService -- --export-openapi > current.json

      - name: Get base schema
        run: git show main:openapi.json > base.json

      - name: Check for breaking changes
        run: |
          npx openapi-diff base.json current.json --fail-on-breaking
  ```

- [ ] **8.7.3** Add load testing:
  ```yaml
  load-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Run k6 load tests
        uses: grafana/k6-action@v0.3.1
        with:
          filename: tests/load/k6-script.js
          flags: --out json=results.json

      - name: Upload k6 results
        uses: actions/upload-artifact@v4
        with:
          name: k6-results
          path: results.json
  ```

- [ ] **8.7.4** Add integration test with full stack:
  ```yaml
  integration-test:
    runs-on: ubuntu-latest
    services:
      ravendb:
        image: ravendb/ravendb:6.0-ubuntu-latest
      minio:
        image: minio/minio:latest
      # ... other services

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - run: dotnet test --filter "Category=Integration"
  ```

- [ ] **8.7.5** Upload test artifacts:
  ```yaml
  - name: Upload coverage report
    uses: actions/upload-artifact@v4
    with:
      name: coverage-report
      path: coverage/

  - name: Upload k6 HTML report
    uses: actions/upload-artifact@v4
    with:
      name: load-test-report
      path: k6-report.html
  ```

- [ ] **8.7.6** Configure staging deployment

- [ ] **8.7.7** Configure production deployment

#### Acceptance Criteria
- Security scans pass
- OpenAPI breaking changes detected
- Load tests run
- Artifacts uploaded

---

## Infrastructure Setup

- [ ] Grafana dashboards (or Aspire UI)
- [ ] Alert rules in monitoring system
- [ ] Kubernetes ConfigMaps for runtime config
- [ ] Admin role in Identity system
- [ ] Log aggregation configured

---

## Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Analytics aggregations | All metric types |
| Integration | Admin endpoints | All operations |
| Integration | Config reload | Hot reload verification |
| E2E | Dashboard data | Accurate metrics display |

---

## Exit Criteria

- [ ] Analytics consume and aggregate events
- [ ] Admin can view/manage users
- [ ] Admin can modify runtime config without restart
- [ ] Dashboards show accurate real-time data
- [ ] Alerts fire correctly on threshold breach
- [ ] Logs searchable by correlation ID
- [ ] CI/CD pipeline complete with security scans
- [ ] Staging and production deployments work

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Alert fatigue | Medium | Proper thresholds, cooldowns |
| Config change breaks service | High | Validation, rollback capability |
| Analytics data volume | Medium | Aggregation, retention policies |

---

## Navigation

← [Phase 7: Optional Features](phase-7-optional-features.md) | [Overview](../overview.md) | [Cross-Cutting Concerns →](../cross-cutting.md)
