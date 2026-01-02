# Security Baseline (`NF-3.x`)

| Concern     | Implementation                                                  |
|-------------|-----------------------------------------------------------------|
| TLS         | Aspire handles locally; document prod ingress requirements      |
| Secrets     | No secrets in repo; use environment variables or secret store   |
| Credentials | Least-privilege service accounts for each dependency            |

## Tasks

- Audit `appsettings.json` for any hardcoded secrets; move to environment variables.
- Document required secrets in `doc/runbooks/secrets.md` (placeholder).
- Ensure Docker Compose / Aspire manifest does not expose credentials.
