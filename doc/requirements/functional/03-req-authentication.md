# Req 1.x — Authentication & Authorization

- **Req 1.1** The system shall allow a Listener to register with `Email`, `DisplayName`, and password (stored as a `PasswordHash`).
- **Req 1.2** The system shall allow a Listener to log in and receive a JWT access token and refresh token (refresh flow).
- **Req 1.3** The system shall enforce user status:
  - `Active`: normal access.
  - `Disabled`: cannot authenticate and/or cannot access protected operations.
  - `PendingDeletion`: only login and streaming are allowed; eligible for cleanup workflows within 30 days.
- **Req 1.4** The system shall authorize API operations by role claim (Listener vs Admin), using the `admin` role claim for Admin authorization.
- **Req 1.5** The system shall support token/session revocation semantics:
  - Logout revokes only the current session.
  - Password change and Admin disable revocation semantics are TBD.

## Clarifications

- **Password policy**: no required minimum length/complexity beyond being non-empty (additional constraints TBD).
- **Password hashing**: use Argon2id (preferred) or bcrypt (acceptable); parameterization is TBD.
- **Email verification**: email confirmation is not required before `Status=Active`.
- **Refresh tokens**:
  - One-time use rotation strategy.
  - TTL: 1 hour.
  - Max concurrent sessions/devices per user: 5.
  - Stored hashed in RavenDB.
