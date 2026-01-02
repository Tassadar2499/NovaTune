# Stage 5 — Track Management + Lifecycle Cleanup

**Goal:** Manage the user library and enforce deletion integrity.

## API Endpoints

- `GET /tracks` list/search/filter/sort with pagination (`Req 6.1`).
- `GET /tracks/{trackId}` details (`Req 6.4`).
- `PATCH /tracks/{trackId}` update title/artist with validation (`Req 6.2`, `NF-6.2` merge policy).
- `DELETE /tracks/{trackId}` soft-delete (`Req 6.3`).
- Optional: `POST /tracks/{trackId}/restore` within grace window (aligned to `NF-6.1`).

## Deletion Semantics

- Publish `TrackDeletedEvent` after state change.
- Invalidate cached presigned URLs immediately.
- Schedule physical deletion after 30 days (configurable; `Req 4.4`, `NF-6.3`).
- Ensure repeatable deletion jobs (safe to re-run; `NF-6.1`).

## Requirements Covered

- `Req 6.x`
- `Req 4.x`
- `NF-6.x`
