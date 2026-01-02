# Aspire Host Composition

Extend `NovaTuneApp.AppHost` to add missing dependencies (`NF-1.1`):

| Dependency | Integration                                    | Notes                                                 |
|------------|------------------------------------------------|-------------------------------------------------------|
| RavenDB    | `Aspire.Hosting.RavenDB` or custom container   | System of record; configure single-node for local dev |
| MinIO      | Custom container resource                      | S3-compatible; create default bucket on startup       |
| Redpanda   | Already present                                | Verify topic auto-creation and notification wiring    |
| Garnet     | Already present                                | Verify AOF persistence enabled                        |

## Tasks

- Add RavenDB container resource with health check.
- Add MinIO container resource with default bucket provisioning.
- Verify existing Garnet and Redpanda configurations meet requirements.
- Ensure all services are independently startable for `NF-1.1` compliance.
