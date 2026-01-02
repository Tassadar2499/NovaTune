# Stage 6 — Playlists

**Goal:** Enable playlist CRUD with stable ordering.

## Data Model

- Playlist doc with:
  - `PlaylistId` (ULID)
  - `UserId`
  - Name
  - Ordered list of track references + positions
- Enforce ownership, limits, and duplicates policy (`Req 7 clarifications`, `NF-2.4` quotas).

## API Endpoints

- Create playlist
- Rename playlist
- Delete playlist
- Add tracks to playlist
- Remove tracks from playlist
- Reorder tracks in playlist

## Requirements Covered

- `Req 7.x`
