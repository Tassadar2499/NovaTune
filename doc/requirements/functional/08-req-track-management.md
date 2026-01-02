# Req 6.x — Track Management (library)

- **Req 6.1** The system shall allow a Listener to list/browse their tracks with pagination and filters (e.g., status, search by title/artist).
- **Req 6.2** The system shall allow a Listener to update permitted metadata fields (`Title`, `Artist`) while enforcing validation constraints (editing while `Status=Processing` is TBD).
- **Req 6.3** The system shall allow a Listener to soft-delete a track (status change) and publish a `TrackDeletedEvent` including:
  - `TrackId`, `UserId`, `Timestamp`, `SchemaVersion`.
- **Req 6.4** The system shall expose track details including processing status and extracted metadata.

## Clarifications

- Sorting/filtering: support sort orders (recent, title, artist) and filters with case-insensitive, partial-match search and status filters (exact API surface TBD).
- Sharing: no sharing across users; ownership is per-user only.
