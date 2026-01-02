# Configuration Validation (`NF-5.1`)

Validate required configuration at startup; fail fast on misconfiguration:

| Setting                | Validation                                         |
|------------------------|----------------------------------------------------|
| `{env}` topic prefix   | Non-empty; matches environment name                |
| Presigned URL TTL      | Positive duration; ≤ 1 hour                        |
| Cache encryption key   | Present for non-dev; meets minimum entropy         |
| Rate limit settings    | Valid numeric thresholds                           |
| Quota limits           | Positive integers for upload size, playlist count  |

## Tasks

- Implement `IStartupFilter` or `IHostedService` that validates configuration on boot.
- Log validation failures with actionable messages.
- Expose validated configuration via `/debug/config` in dev only (redacted).
