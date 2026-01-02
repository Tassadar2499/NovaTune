# Req 7.x — Playlists (declared in stack; not yet modeled)

- **Req 7.1** The system shall allow a Listener to create, rename, and delete playlists.
- **Req 7.2** The system shall allow a Listener to add/remove/reorder tracks within a playlist.
- **Req 7.3** The system shall persist playlists in RavenDB and enforce ownership and authorization rules.

## Clarifications

- Constraints: max playlists per user and max tracks per playlist are required but values are TBD/configurable.
- Duplicates: duplicate tracks in a playlist are allowed.
- Ordering: playlists require stable ordering with explicit positions.
- Future: the data model should anticipate playlist sharing/collaboration (while remaining private by default for now).
