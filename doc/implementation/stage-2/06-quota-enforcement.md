# 6. Quota Enforcement (Req 2 clarifications, NF-2.4)

## Per-User Quotas (Configurable)

| Quota | Default | Enforcement Point |
|-------|---------|-------------------|
| `MaxStorageBytes` | 1 GB | Upload initiation |
| `MaxTrackCount` | 500 | Upload initiation |
| `MaxFileSizeBytes` | 100 MB | Upload initiation + worker validation |

## Storage Tracking

- Maintain `User.UsedStorageBytes` aggregate in RavenDB
- Increment on Track creation (worker)
- Decrement on Track physical deletion (lifecycle worker)
- Use optimistic concurrency for updates
