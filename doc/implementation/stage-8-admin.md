# Stage 8 — Admin / Moderation + Audit Logs

**Goal:** Allow administrative operations with auditability.

## Admin APIs

- Search/list users; update status (`Req 11.1`).
- Search/list tracks across users; delete/moderate with reason codes (`Req 11.2`).
- View analytics dashboards (`Req 11.3`).

## Audit Logging

- Record:
  - Actor identity
  - Timestamp
  - Action
  - Target
  - Reason codes (`NF-3.5`)
- Retention 1 year and access restricted to Admin role.
- Tamper-evidence mechanism remains TBD (track as an explicit open item).

## Requirements Covered

- `Req 11.x`
- `NF-3.5`
