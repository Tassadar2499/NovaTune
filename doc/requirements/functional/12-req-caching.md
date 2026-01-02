# Req 10.x — Session, Token, and URL Caching

- **Req 10.1** The system shall cache session/token-related data (refresh flow, revocation flags) in Garnet/Redis with TTL (what is cached vs stored in RavenDB is TBD).
- **Req 10.2** The system shall cache presigned URLs keyed by user+track and invalidate them at minimum on track deletion and logout (additional triggers TBD).
- **Req 10.3** Cache entries that include full presigned URLs shall be encrypted at rest.
- **Req 10.4** Cache behavior (key prefix and TTLs) shall be configurable via app configuration (presigned URL TTLs are TBD).
