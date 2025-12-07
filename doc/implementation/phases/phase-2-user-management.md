# Phase 2: User Management (FR 1.x)

> **Status:** ⏳ Pending
> **Dependencies:** Phase 1 (Infrastructure)
> **Milestone:** M1 - Foundation

## Objective

Implement complete user lifecycle management with secure authentication using ASP.NET Identity backed by RavenDB.

---

## FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 1.1 | Account Creation | P1 | Test |
| FR 1.2 | Authentication | P1 | Test |
| FR 1.3 | Profile Updates | P2 | Test |
| FR 1.4 | Account Removal | P1 | Test |

## NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-3.2 | Data Protection | RavenDB encryption at rest |
| NF-3.3 | Privacy & Data Subject Rights | Cascade deletion implementation |
| NF-3.4 | Authentication Performance | JWT <200ms p95 |
| NF-6.2 | RavenDB Integrity | User document schema, indexes |

---

## Tasks

### Task 2.1: RavenDB Identity Store Implementation

**Priority:** P1 (Must-have)

Implement custom ASP.NET Identity stores backed by RavenDB.

#### Subtasks

- [ ] **2.1.1** Create `ApplicationUser` entity extending Identity:
  ```csharp
  public sealed class ApplicationUser
  {
      public string Id { get; init; } = $"Users/{Guid.NewGuid()}";
      public string Email { get; set; } = string.Empty;
      public string NormalizedEmail { get; set; } = string.Empty;
      public string? UserName { get; set; }
      public string? NormalizedUserName { get; set; }
      public string PasswordHash { get; set; } = string.Empty;
      public string? SecurityStamp { get; set; }
      public string? ConcurrencyStamp { get; set; }
      public string DisplayName { get; set; } = string.Empty;
      public string? AvatarUrl { get; set; }
      public bool EmailConfirmed { get; set; }
      public bool LockoutEnabled { get; set; } = true;
      public DateTimeOffset? LockoutEnd { get; set; }
      public int AccessFailedCount { get; set; }
      public UserStatus Status { get; set; } = UserStatus.Active;
      public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
      public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
      public DateTimeOffset? DeletedAt { get; set; }
  }
  ```

- [ ] **2.1.2** Implement `RavenUserStore<ApplicationUser>`:
  - `IUserStore<T>` - Basic user CRUD
  - `IUserPasswordStore<T>` - Password management
  - `IUserEmailStore<T>` - Email operations
  - `IUserLockoutStore<T>` - Lockout handling
  - `IUserSecurityStampStore<T>` - Security stamp
  - Use optimistic concurrency with RavenDB change vectors

- [ ] **2.1.3** Implement `RavenRoleStore<ApplicationRole>`:
  ```csharp
  public sealed class ApplicationRole
  {
      public string Id { get; init; } = $"Roles/{Guid.NewGuid()}";
      public string Name { get; set; } = string.Empty;
      public string NormalizedName { get; set; } = string.Empty;
  }
  ```

- [ ] **2.1.4** Create RavenDB indexes:
  ```csharp
  public class Users_ByEmail : AbstractIndexCreationTask<ApplicationUser>
  {
      public Users_ByEmail()
      {
          Map = users => from user in users
                         select new { user.NormalizedEmail };
      }
  }

  public class Users_ByStatus : AbstractIndexCreationTask<ApplicationUser>
  {
      public Users_ByStatus()
      {
          Map = users => from user in users
                         select new { user.Status, user.CreatedAt };
      }
  }
  ```

- [ ] **2.1.5** Configure Identity options:
  ```csharp
  services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
  {
      // Password requirements
      options.Password.RequiredLength = 12;
      options.Password.RequireDigit = true;
      options.Password.RequireLowercase = true;
      options.Password.RequireUppercase = true;
      options.Password.RequireNonAlphanumeric = true;

      // Lockout settings
      options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
      options.Lockout.MaxFailedAccessAttempts = 5;
      options.Lockout.AllowedForNewUsers = true;

      // User requirements
      options.User.RequireUniqueEmail = true;
  });
  ```

- [ ] **2.1.6** Write comprehensive unit tests for all store methods

#### Acceptance Criteria
- All Identity interfaces implemented
- RavenDB stores pass Identity conformance tests
- Optimistic concurrency works correctly
- Unit test coverage ≥80%

---

### Task 2.2: JWT Authentication Implementation

**Priority:** P1 (Must-have)

Implement JWT-based authentication with refresh tokens.

#### Subtasks

- [ ] **2.2.1** Configure asymmetric JWT signing (RS256):
  ```csharp
  public interface IJwtService
  {
      Task<TokenPair> GenerateTokensAsync(ApplicationUser user);
      Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string token);
      Task<TokenPair?> RefreshTokensAsync(string refreshToken);
      Task RevokeRefreshTokenAsync(string refreshToken);
  }
  ```

- [ ] **2.2.2** Create `JwtService` implementation:
  ```csharp
  public sealed class JwtService : IJwtService
  {
      private readonly RSA _privateKey;
      private readonly JwtSecurityTokenHandler _tokenHandler;

      public async Task<TokenPair> GenerateTokensAsync(ApplicationUser user)
      {
          var accessToken = CreateAccessToken(user);  // 15 min TTL
          var refreshToken = await CreateRefreshTokenAsync(user);  // 7 day sliding
          return new TokenPair(accessToken, refreshToken);
      }

      private string CreateAccessToken(ApplicationUser user)
      {
          var claims = new[]
          {
              new Claim(JwtRegisteredClaimNames.Sub, user.Id),
              new Claim(JwtRegisteredClaimNames.Email, user.Email),
              new Claim("displayName", user.DisplayName),
              new Claim(JwtRegisteredClaimNames.Iat,
                  DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
              new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
          };

          var key = new RsaSecurityKey(_privateKey) { KeyId = "key-2024-01" };
          var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

          var token = new JwtSecurityToken(
              issuer: _options.Issuer,
              audience: _options.Audience,
              claims: claims,
              expires: DateTime.UtcNow.AddMinutes(15),
              signingCredentials: credentials);

          return _tokenHandler.WriteToken(token);
      }
  }
  ```

- [ ] **2.2.3** Create `RefreshToken` entity:
  ```csharp
  public sealed class RefreshToken
  {
      public string Id { get; init; } = $"RefreshTokens/{Guid.NewGuid()}";
      public string UserId { get; init; } = string.Empty;
      public string TokenHash { get; init; } = string.Empty;
      public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
      public DateTimeOffset ExpiresAt { get; init; }
      public DateTimeOffset? RevokedAt { get; set; }
      public string? ReplacedByTokenId { get; set; }
      public string? DeviceInfo { get; set; }
      public string? IpAddress { get; set; }
  }
  ```

- [ ] **2.2.4** Implement JWKS endpoint at `/.well-known/jwks.json`:
  ```csharp
  app.MapGet("/.well-known/jwks.json", (IJwtService jwtService) =>
  {
      var jwks = new JsonWebKeySet();
      jwks.Keys.Add(jwtService.GetPublicKey());
      return Results.Json(jwks);
  });
  ```

- [ ] **2.2.5** Configure JWT validation:
  ```csharp
  services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options =>
      {
          options.TokenValidationParameters = new TokenValidationParameters
          {
              ValidateIssuer = true,
              ValidIssuer = configuration["Jwt:Issuer"],
              ValidateAudience = true,
              ValidAudience = configuration["Jwt:Audience"],
              ValidateLifetime = true,
              ClockSkew = TimeSpan.FromMinutes(2),
              ValidateIssuerSigningKey = true,
              IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
                  keyResolver.GetSigningKeys(kid)
          };
      });
  ```

- [ ] **2.2.6** Implement token revocation via NCache blocklist

- [ ] **2.2.7** Add concurrent session limit (5 per user)

- [ ] **2.2.8** Write unit tests for all JWT operations (100% coverage)

#### Acceptance Criteria
- Access tokens use RS256 signing
- Refresh tokens support 7-day sliding expiration
- Token revocation works within 5 seconds
- JWKS endpoint returns valid public keys
- JWT validation <200ms p95

---

### Task 2.3: Authentication Endpoints

**Priority:** P1 (Must-have)

Implement authentication API endpoints.

#### Subtasks

- [ ] **2.3.1** Create `POST /api/v1/auth/register`:
  ```csharp
  public record RegisterRequest(
      string Email,
      string Password,
      string DisplayName);

  public record AuthResponse(
      string AccessToken,
      string RefreshToken,
      DateTimeOffset ExpiresAt,
      UserDto User);

  app.MapPost("/api/v1/auth/register", async (
      RegisterRequest request,
      UserManager<ApplicationUser> userManager,
      IJwtService jwtService) =>
  {
      // Validate request
      // Create user
      // Generate verification email
      // Return tokens
  });
  ```

- [ ] **2.3.2** Create `POST /api/v1/auth/login`:
  ```csharp
  public record LoginRequest(string Email, string Password);

  app.MapPost("/api/v1/auth/login", async (
      LoginRequest request,
      SignInManager<ApplicationUser> signInManager,
      IJwtService jwtService) =>
  {
      // Validate credentials
      // Check lockout
      // Generate tokens
      // Record login attempt
  });
  ```

- [ ] **2.3.3** Create `POST /api/v1/auth/refresh`:
  ```csharp
  public record RefreshRequest(string RefreshToken);

  app.MapPost("/api/v1/auth/refresh", async (
      RefreshRequest request,
      IJwtService jwtService) =>
  {
      // Validate refresh token
      // Check if revoked
      // Generate new token pair
      // Rotate refresh token
  });
  ```

- [ ] **2.3.4** Create `POST /api/v1/auth/logout`:
  ```csharp
  app.MapPost("/api/v1/auth/logout", async (
      HttpContext context,
      IJwtService jwtService) =>
  {
      // Extract tokens from request
      // Revoke refresh token
      // Add access token to blocklist
  }).RequireAuthorization();
  ```

- [ ] **2.3.5** Add request validation with FluentValidation

- [ ] **2.3.6** Add rate limiting: 10 attempts per minute per IP

- [ ] **2.3.7** Write integration tests for all auth flows

#### Acceptance Criteria
- All endpoints return proper status codes
- Error messages are secure (no info leakage)
- Rate limiting prevents brute force
- Integration tests cover happy and error paths

---

### Task 2.4: Profile Management Endpoints

**Priority:** P2 (Should-have)

Implement user profile CRUD operations.

#### Subtasks

- [ ] **2.4.1** Create `GET /api/v1/users/me`:
  ```csharp
  public record UserDto(
      string Id,
      string Email,
      string DisplayName,
      string? AvatarUrl,
      bool EmailConfirmed,
      DateTimeOffset CreatedAt);

  app.MapGet("/api/v1/users/me", async (
      ClaimsPrincipal user,
      IUserService userService) =>
  {
      var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
      var userData = await userService.GetByIdAsync(userId);
      return Results.Ok(userData.ToDto());
  }).RequireAuthorization();
  ```

- [ ] **2.4.2** Create `PATCH /api/v1/users/me`:
  ```csharp
  public record UpdateProfileRequest(
      string? DisplayName,
      string? CurrentPassword,
      string? NewPassword);

  app.MapPatch("/api/v1/users/me", async (
      UpdateProfileRequest request,
      ClaimsPrincipal user,
      IUserService userService) =>
  {
      // Validate request
      // Check current password if changing password
      // Update user
      // Handle optimistic concurrency
  }).RequireAuthorization();
  ```

- [ ] **2.4.3** Create `POST /api/v1/users/me/avatar`:
  ```csharp
  app.MapPost("/api/v1/users/me/avatar", async (
      IFormFile file,
      ClaimsPrincipal user,
      IStorageService storage,
      IUserService userService) =>
  {
      // Validate file type (JPEG, PNG, WebP)
      // Validate file size (<5MB)
      // Resize if needed
      // Upload to MinIO
      // Update user avatar URL
  }).RequireAuthorization()
    .DisableAntiforgery();
  ```

- [ ] **2.4.4** Add optimistic concurrency with ETags

- [ ] **2.4.5** Write tests for profile operations

#### Acceptance Criteria
- Profile retrieval returns correct data
- Updates use optimistic concurrency
- Avatar upload validates and resizes images
- All endpoints require authentication

---

### Task 2.5: Account Deletion (Cascade)

**Priority:** P1 (Must-have)

Implement account deletion with cascade cleanup.

#### Subtasks

- [ ] **2.5.1** Create `DELETE /api/v1/users/me`:
  ```csharp
  app.MapDelete("/api/v1/users/me", async (
      ClaimsPrincipal user,
      DeleteAccountRequest request,
      IUserService userService) =>
  {
      // Verify password
      // Initiate soft delete
      // Publish deletion event
      // Invalidate all sessions
  }).RequireAuthorization();
  ```

- [ ] **2.5.2** Implement soft-delete with 30-day recovery:
  ```csharp
  public async Task SoftDeleteAsync(string userId)
  {
      var user = await _session.LoadAsync<ApplicationUser>(userId);
      user.Status = UserStatus.PendingDeletion;
      user.DeletedAt = DateTimeOffset.UtcNow;
      user.UpdatedAt = DateTimeOffset.UtcNow;

      await _session.SaveChangesAsync();
      await _eventPublisher.PublishAsync(new UserDeletionInitiated(userId));
  }
  ```

- [ ] **2.5.3** Create Kafka tombstone event:
  ```csharp
  public record UserDeletionInitiated(
      string UserId,
      DateTimeOffset InitiatedAt,
      DateTimeOffset ScheduledPurgeAt);
  ```

- [ ] **2.5.4** Implement NCache session invalidation

- [ ] **2.5.5** Create recovery endpoint `POST /api/v1/users/me/recover`

- [ ] **2.5.6** Document cascade deletion steps (stub for Phase 4+ items):
  - Revoke all refresh tokens ✓
  - Invalidate NCache entries ✓
  - Mark user as pending deletion ✓
  - Publish Kafka event ✓
  - Schedule MinIO object deletion (Phase 4)
  - Remove from playlists (Phase 7)

- [ ] **2.5.7** Write integration tests for deletion cascade

#### Acceptance Criteria
- Soft delete sets proper status
- Kafka event published within 5 seconds
- All sessions invalidated immediately
- Recovery works within 30-day window

---

### Task 2.6: Email Verification Flow

**Priority:** P2 (Should-have)

Implement email verification for new accounts.

#### Subtasks

- [ ] **2.6.1** Create email verification token:
  ```csharp
  public interface IEmailService
  {
      Task SendVerificationEmailAsync(ApplicationUser user);
      Task<bool> VerifyEmailAsync(string userId, string token);
  }
  ```

- [ ] **2.6.2** Create `POST /api/v1/auth/verify-email`:
  ```csharp
  app.MapPost("/api/v1/auth/verify-email", async (
      VerifyEmailRequest request,
      IEmailService emailService) =>
  {
      var success = await emailService.VerifyEmailAsync(
          request.UserId,
          request.Token);
      return success
          ? Results.Ok()
          : Results.BadRequest("Invalid or expired token");
  });
  ```

- [ ] **2.6.3** Create `POST /api/v1/auth/resend-verification`

- [ ] **2.6.4** Configure email service (SMTP or SendGrid)

- [ ] **2.6.5** Create email templates

- [ ] **2.6.6** Add RabbitMQ queue for async email delivery

#### Acceptance Criteria
- Verification emails sent on registration
- Tokens expire after 24 hours
- Resend limits prevent abuse

---

### Task 2.7: Authentication Observability

**Priority:** P2 (Should-have)

Add metrics and logging for authentication operations.

#### Subtasks

- [ ] **2.7.1** Add authentication metrics:
  ```csharp
  // Metrics to implement
  novatune_auth_login_total{status="success|failure|lockout"}
  novatune_auth_register_total{status="success|failure"}
  novatune_auth_token_refresh_total{status="success|failure|revoked"}
  novatune_auth_session_count{userId_hash="..."}
  ```

- [ ] **2.7.2** Add structured logging:
  ```csharp
  _logger.LogInformation(
      "User login attempt. Email={Email}, Result={Result}, IP={IpAddress}",
      request.Email.Masked(),
      result,
      context.Connection.RemoteIpAddress);
  ```

- [ ] **2.7.3** Add distributed tracing spans for auth operations

- [ ] **2.7.4** Create Aspire dashboard for auth metrics

#### Acceptance Criteria
- All auth operations emit metrics
- Logs include correlation IDs
- No sensitive data in logs

---

## Infrastructure Setup

- [ ] RavenDB database and collections: `Users`, `RefreshTokens`
- [ ] RavenDB indexes: `Users_ByEmail`, `Users_ByStatus`
- [ ] NCache region: `auth-tokens`
- [ ] Email service integration (SMTP or SendGrid)
- [ ] RSA key pair for JWT signing

---

## Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Identity stores | ≥80% |
| Unit | JWT service | 100% |
| Integration | Auth endpoints | All happy/error paths |
| Integration | Token refresh flow | Expiry, revocation |
| Integration | Account deletion cascade | Verify all cleanup steps |

---

## Exit Criteria

- [ ] User can register, login, refresh, and logout
- [ ] JWT tokens validate correctly with proper TTLs
- [ ] Account lockout triggers after 5 failed logins
- [ ] Profile CRUD operations work with optimistic concurrency
- [ ] Account deletion initiates cascade (stubs for Phase 4+ items)
- [ ] All auth endpoints return <200ms p95
- [ ] ≥80% test coverage for auth middleware

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| RavenDB Identity store complexity | High | Reference existing implementations, extensive tests |
| Token revocation latency | Medium | NCache with sub-5s propagation |
| Email delivery delays | Low | Async processing, retry with RabbitMQ |

---

## Navigation

← [Phase 1: Infrastructure](phase-1-infrastructure.md) | [Overview](../overview.md) | [Phase 3: Audio Upload →](phase-3-audio-upload.md)
