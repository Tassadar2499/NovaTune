# Stage 1 — Authentication & Authorization

**Goal:** Enable secure identity and role separation for Listener vs Admin, implementing JWT-based authentication with refresh token rotation and session management.

## Prerequisites

- Stage 0 completed: RavenDB, Garnet, and observability baseline operational.

## API Endpoints

| Endpoint            | Method | Description                                                      | Auth Required |
|---------------------|--------|------------------------------------------------------------------|---------------|
| `/auth/register`    | POST   | Register new Listener with email, display name, password         | No            |
| `/auth/login`       | POST   | Authenticate and receive JWT access + refresh tokens             | No            |
| `/auth/refresh`     | POST   | Exchange refresh token for new token pair (one-time use)         | No            |
| `/auth/logout`      | POST   | Revoke current session only                                      | Yes           |

### Request/Response Contracts

**POST /auth/register** (`Req 1.1`)

Request:
```json
{
  "email": "user@example.com",
  "displayName": "User Name",
  "password": "securepassword"
}
```

Response 201 Created:
```json
{
  "userId": "01HQ3K...",
  "email": "user@example.com",
  "displayName": "User Name"
}
```

**POST /auth/login** (`Req 1.2`)

Request:
```json
{
  "email": "user@example.com",
  "password": "securepassword"
}
```

Response 200 OK:
```json
{
  "accessToken": "eyJhbG...",
  "refreshToken": "base64...",
  "expiresIn": 900,
  "tokenType": "Bearer"
}
```

**POST /auth/refresh**

Request:
```json
{
  "refreshToken": "base64..."
}
```

Response 200 OK: Same shape as login response.

**POST /auth/logout** (`Req 1.5`)

Response 204 No Content (empty body).

## Identity Store Implementation

### RavenDB Document Models

Implement custom ASP.NET Identity stores backed by RavenDB (`stack.md`):

| Document Type        | Collection           | Purpose                                         |
|----------------------|----------------------|-------------------------------------------------|
| `ApplicationUser`    | `Users`              | User profile, credentials, status, roles        |
| `RefreshToken`       | `RefreshTokens`      | Hashed refresh tokens with metadata             |

**ApplicationUser Schema:**
```csharp
public class ApplicationUser
{
    public string Id { get; set; }              // RavenDB internal ID
    public string UserId { get; set; }          // ULID (external identifier)
    public string Email { get; set; }
    public string NormalizedEmail { get; set; }
    public string DisplayName { get; set; }
    public string PasswordHash { get; set; }    // Argon2id hash
    public UserStatus Status { get; set; }      // Active, Disabled, PendingDeletion
    public List<string> Roles { get; set; }     // "Listener", "Admin"
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
```

**RefreshToken Schema:**
```csharp
public class RefreshToken
{
    public string Id { get; set; }              // RavenDB internal ID
    public string UserId { get; set; }          // References ApplicationUser.UserId
    public string TokenHash { get; set; }       // SHA-256 hash of token
    public string DeviceIdentifier { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
}
```

### ASP.NET Identity Stores

Tasks:
- Implement `IUserStore<ApplicationUser>` with RavenDB document session.
- Implement `IUserPasswordStore<ApplicationUser>` for credential validation.
- Implement `IUserRoleStore<ApplicationUser>` for role management.
- Do NOT implement `IRoleStore` (roles are embedded in user document).

## Password Security (`Req 1.x clarifications`)

| Setting                | Value                                      |
|------------------------|--------------------------------------------|
| Hashing algorithm      | Argon2id (preferred) or bcrypt (fallback)  |
| Argon2 memory cost     | 64 MB (configurable)                       |
| Argon2 iterations      | 3 (configurable)                           |
| Argon2 parallelism     | 4 (configurable)                           |
| Minimum password length| Non-empty (additional constraints TBD)     |

Tasks:
- Integrate `Isopoh.Cryptography.Argon2` or equivalent NuGet package.
- Implement `IPasswordHasher<ApplicationUser>` using Argon2id.
- Make Argon2 parameters configurable via `appsettings.json`.

## JWT Configuration (`Req 1.2`)

| Setting              | Value                                          |
|----------------------|------------------------------------------------|
| Access token TTL     | 15 minutes                                     |
| Signing algorithm    | RS256 (asymmetric) or HS256 (symmetric)        |
| Issuer               | Configurable per environment                   |
| Audience             | Configurable per environment                   |
| Claims               | `sub` (UserId), `email`, `roles`, `jti`        |

Tasks:
- Configure `JwtBearerAuthentication` in `Program.cs`.
- Store signing keys securely (environment variables or secret store per `NF-3.4`).
- Include `jti` (JWT ID) claim for potential revocation support.
- Add `admin` role claim for Admin authorization (`Req 1.4`).

## Refresh Token Management (`Req 1.x clarifications`)

| Setting                    | Value                                    |
|----------------------------|------------------------------------------|
| Refresh token TTL          | 1 hour                                   |
| Rotation strategy          | One-time use (invalidate on exchange)    |
| Max concurrent sessions    | 5 per user                               |
| Token storage              | SHA-256 hash in RavenDB                  |

Tasks:
- Generate cryptographically secure refresh tokens (256 bits).
- Store only SHA-256 hash in RavenDB (`NF-3.2`).
- On refresh: validate hash, issue new pair, revoke old token atomically.
- Enforce session limit: reject new login if 5 active sessions exist (or evict oldest).
- Include device identifier in refresh token record for session management.

## User Status Enforcement (`Req 1.3`)

| Status            | Login | Refresh | Protected APIs | Streaming |
|-------------------|-------|---------|----------------|-----------|
| `Active`          | ✓     | ✓       | ✓              | ✓         |
| `Disabled`        | ✗     | ✗       | ✗              | ✗         |
| `PendingDeletion` | ✓     | ✓       | ✗              | ✓         |

Tasks:
- Check status on login; return 403 if `Disabled`.
- Add authorization policy for `PendingDeletion` users (streaming only).
- Implement status check middleware or policy requirement.

## Authorization Policies (`Req 1.4`)

Define authorization policies for role-based access:

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("Listener", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("Admin", policy =>
        policy.RequireClaim("roles", "admin"));

    options.AddPolicy("ActiveUser", policy =>
        policy.Requirements.Add(new ActiveUserRequirement()));
});
```

Tasks:
- Implement `IAuthorizationRequirement` for status-based policies.
- Apply `[Authorize(Policy = "...")]` to controllers.

## Rate Limiting (`Req 8.2`, `NF-2.5`)

Configure per-endpoint rate limits using `System.Threading.RateLimiting`:

| Endpoint         | Limit (per IP)   | Limit (per account) | Window   |
|------------------|------------------|---------------------|----------|
| `/auth/login`    | 10 requests      | 5 requests          | 1 minute |
| `/auth/register` | 10 requests      | N/A                 | 1 minute |
| `/auth/refresh`  | 20 requests      | N/A                 | 1 minute |

Tasks:
- Configure sliding window rate limiters per endpoint.
- Return HTTP 429 with `Retry-After` header on limit exceeded.
- Make limits configurable via `appsettings.json`.
- Log rate limit violations with client IP and endpoint.

## Latency Budgets (`NF-2.2`)

| Endpoint         | p95 Target | p99 Target |
|------------------|------------|------------|
| `/auth/login`    | 400ms      | 1200ms     |
| `/auth/refresh`  | 400ms      | 1200ms     |
| `/auth/register` | 400ms      | 1200ms     |

Tasks:
- Apply dependency timeouts (RavenDB: 5s per `stage-0`).
- Add histogram metrics for auth endpoint latencies.
- Alert on sustained p99 violations (future observability stage).

## Error Handling (`Req 8.1`)

All auth endpoints return RFC 7807 Problem Details:

```json
{
  "type": "https://novatune.example/errors/invalid-credentials",
  "title": "Invalid Credentials",
  "status": 401,
  "detail": "The email or password provided is incorrect.",
  "instance": "/auth/login",
  "traceId": "00-abc123..."
}
```

| Scenario                  | Status | Type Suffix               |
|---------------------------|--------|---------------------------|
| Invalid credentials       | 401    | `invalid-credentials`     |
| Account disabled          | 403    | `account-disabled`        |
| Account pending deletion  | 403    | `account-pending-deletion`|
| Email already registered  | 409    | `email-exists`            |
| Refresh token expired     | 401    | `token-expired`           |
| Refresh token invalid     | 401    | `invalid-token`           |
| Rate limit exceeded       | 429    | `rate-limit-exceeded`     |
| Validation errors         | 400    | `validation-error`        |

Tasks:
- Implement `ProblemDetails` factory for consistent error responses.
- Configure `ApiBehaviorOptions.InvalidModelStateResponseFactory`.
- Include `traceId` from OpenTelemetry context.

## Observability Integration

Leverage Stage 0 observability infrastructure:

| Concern           | Implementation                                           |
|-------------------|----------------------------------------------------------|
| Logging           | Structured logs with `CorrelationId`, user context       |
| Metrics           | `auth_login_total`, `auth_login_duration_seconds`, etc.  |
| Tracing           | Spans for RavenDB queries, password hashing              |
| Redaction         | Never log passwords, tokens, or hashes (`NF-4.5`)        |

Tasks:
- Add custom metrics for auth operations (success/failure counts).
- Add tracing spans for identity store operations.
- Ensure password and token values are excluded from logs.

## Security Considerations (`NF-3.x`)

| Concern                   | Mitigation                                             |
|---------------------------|--------------------------------------------------------|
| Brute force attacks       | Rate limiting per IP and account                       |
| Token leakage             | Short TTL, refresh rotation, secure storage            |
| Timing attacks            | Constant-time password comparison (Argon2 default)     |
| Session fixation          | New tokens on login, invalidate old on refresh         |
| CSRF                      | Bearer tokens (not cookies) eliminate CSRF risk        |

Tasks:
- Audit all auth endpoints for information disclosure.
- Ensure error messages don't reveal account existence on login.
- Log failed authentication attempts for security monitoring.

## Testing Strategy

### Unit Tests
- Password hashing: verify hash generation and validation.
- Token generation: verify claims, expiry, signature.
- Refresh rotation: verify old token invalidation.
- Status enforcement: verify policy evaluation per status.
- Rate limit logic: verify window calculations.

### Integration Tests
- Full registration → login → refresh → logout flow.
- Session limit enforcement (6th login rejected/evicts oldest).
- Disabled user cannot authenticate.
- Rate limit returns 429 after threshold.
- Invalid refresh token returns 401.

Tasks:
- Add unit tests in `src/unit_tests/` for auth domain logic.
- Add integration tests in `src/integration_tests/` using Aspire test host.

## Claude Skills

The following Claude Code skills are available to assist with implementation:

| Skill | Description | Usage |
|-------|-------------|-------|
| `add-auth-endpoint` | Create authentication endpoints with proper DTOs, services, and error handling | Auth controller setup |
| `configure-jwt` | Set up JWT Bearer authentication with token generation and validation | JWT configuration |
| `add-ravendb-identity-store` | Implement ASP.NET Identity stores backed by RavenDB | User and token persistence |
| `add-rate-limiting` | Configure per-endpoint rate limiting with sliding windows | Rate limit setup |
| `add-api-endpoint` | General minimal API endpoint creation | Supporting endpoints |
| `add-entity-field` | Add fields to existing entity models | Model extensions |
| `build-and-run` | Build, test, and run the application | Development workflow |

### Skill Usage Guide

**Phase 1: Identity Foundation**
1. Use `add-ravendb-identity-store` to create `ApplicationUser` model and RavenDB stores
2. Use `configure-jwt` to set up token generation and authentication middleware

**Phase 2: Auth Endpoints**
1. Use `add-auth-endpoint` to create register, login, refresh, logout endpoints
2. Use `add-rate-limiting` to apply rate limits to auth endpoints

**Phase 3: Verification**
1. Use `build-and-run` to verify the implementation compiles and tests pass

### Invoking Skills

To invoke a skill, ask Claude to use a specific skill:

```
Use the configure-jwt skill to set up JWT authentication
```

Or reference this plan:

```
Implement Phase 1 from stage-1-authentication.md using the relevant skills
```

## Acceptance Criteria

- [ ] `POST /auth/register` creates user with `Active` status and hashed password.
- [ ] `POST /auth/login` returns JWT access token (15min TTL) and refresh token (1h TTL).
- [ ] `POST /auth/refresh` rotates tokens and invalidates the used refresh token.
- [ ] `POST /auth/logout` revokes only the current session.
- [ ] Disabled users receive 403 on login attempt.
- [ ] PendingDeletion users can login but cannot access non-streaming endpoints.
- [ ] Admin role claim enables Admin-only endpoint access.
- [ ] Rate limits return 429 with `Retry-After` header.
- [ ] Auth endpoints meet p95 < 400ms latency target.
- [ ] Passwords and tokens are never logged.
- [ ] All errors return RFC 7807 Problem Details format.

## Open Items

- **Password complexity policy**: Currently non-empty only; define minimum requirements.
- **Password change flow**: Revocation semantics TBD (`Req 1.5`).
- **Admin disable revocation**: Whether to invalidate all sessions immediately TBD.
- **Email verification**: Not required for MVP; consider for future stage.
- **Account lockout**: Consider temporary lockout after N failed attempts (beyond rate limiting).
- **Argon2 vs bcrypt**: Confirm Argon2id library compatibility with .NET 9.

## Requirements Covered

| Requirement | Description                                          |
|-------------|------------------------------------------------------|
| `Req 1.1`   | Listener registration with email/password            |
| `Req 1.2`   | JWT access token + refresh token flow                |
| `Req 1.3`   | User status enforcement (Active/Disabled/Pending)    |
| `Req 1.4`   | Role-based authorization (Listener vs Admin)         |
| `Req 1.5`   | Session revocation (logout)                          |
| `Req 8.1`   | RFC 7807 Problem Details error format                |
| `Req 8.2`   | Rate limiting for auth endpoints                     |
| `NF-2.2`    | Latency budgets (p95/p99 targets)                    |
| `NF-2.5`    | Configurable rate limits with 429 response           |
| `NF-3.2`    | Secure token storage (hashed refresh tokens)         |
| `NF-3.4`    | No secrets in repo; least-privilege credentials      |
| `NF-4.5`    | Log redaction (passwords, tokens)                    |
