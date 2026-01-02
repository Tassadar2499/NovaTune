# Stage 1 — Authentication & Authorization

**Goal:** Enable secure identity and role separation for Listener vs Admin.

## API Endpoints

- `POST /auth/register` (Req 1.1)
- `POST /auth/login` (Req 1.2)
- `POST /auth/refresh` (refresh rotation; one-time use; TTL 1h)
- `POST /auth/logout` (revokes current session; Req 1.5)

## Identity Store

- Implement ASP.NET Identity with RavenDB stores (`stack.md`).
- Persist hashed refresh tokens (one-time rotation) + session limits (max 5 devices).
- Enforce `UserStatus` rules (`Req 1.3`).

## Rate Limits

- Rate limits for auth endpoints (`Req 8.2`, `NF-2.5`).

## Requirements Covered

- `Req 1.x`
- `NF-3.x`
- `NF-2.2`
- `NF-2.5`
