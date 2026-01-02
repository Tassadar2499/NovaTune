# Req 8.x — Error Handling & Rate Limiting

- **Req 8.1** The API shall return errors using RFC 7807 Problem Details for validation, authentication, authorization, and not-found failures.
- **Req 8.2** The system shall implement explicit rate limits for:
  - Login
  - Upload initiation
  - Playback URL issuance
  - Telemetry ingestion
  (exact limits TBD/configurable)
