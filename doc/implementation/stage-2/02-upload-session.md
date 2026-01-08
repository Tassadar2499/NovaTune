# 2. Server-Side Correlation State: UploadSession

## Document Schema (RavenDB)

```csharp
public sealed class UploadSession
{
    public string Id { get; init; }                  // RavenDB doc ID (internal)
    public required string UploadId { get; init; }   // ULID (external identifier)
    public required string UserId { get; init; }     // ULID
    public required string ReservedTrackId { get; init; } // ULID (pre-allocated)
    public required string ObjectKey { get; init; }  // MinIO object key
    public required string ExpectedMimeType { get; init; }
    public required long MaxAllowedSizeBytes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public UploadSessionStatus Status { get; set; } = UploadSessionStatus.Pending;

    // Optional: client-provided metadata to carry forward to Track
    public string? Title { get; init; }
    public string? Artist { get; init; }
}

public enum UploadSessionStatus
{
    Pending,    // Awaiting upload
    Completed,  // MinIO notification received, Track created
    Expired,    // TTL passed without completion
    Failed      // Validation failed on notification
}
```

## ObjectKey Generation (Req 2.3)

Format: `audio/{userId}/{trackId}/{randomSuffix}`

- `randomSuffix`: 16-byte base64url-encoded value for guess-resistance
- Example: `audio/01HXK.../01HXK.../a1B2c3D4e5F6g7H8`

## Session Expiry & Cleanup

- Default TTL: 15 minutes (configurable)
- **Cleanup job**: Background task marks expired sessions as `Expired` and deletes them after 24 hours
- RavenDB index: `UploadSessions_ByStatusAndExpiry` for efficient queries
