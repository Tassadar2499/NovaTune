# 6. Configuration

## `StreamingOptions`

```csharp
public class StreamingOptions
{
    public const string SectionName = "Streaming";

    /// <summary>
    /// Presigned URL expiry duration.
    /// Default: 2 minutes (dev), 60-120 seconds (prod).
    /// </summary>
    public TimeSpan PresignExpiry { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Cache TTL buffer (subtracted from presign expiry).
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan CacheTtlBuffer { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Rate limit: requests per minute per user.
    /// Default: 60.
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 60;
}
```

## `appsettings.json` Example

```json
{
  "Streaming": {
    "PresignExpiry": "00:02:00",
    "CacheTtlBuffer": "00:00:30",
    "RateLimitPerMinute": 60
  },
  "CacheEncryption": {
    "KeyVersion": "v1",
    "Algorithm": "AES-256-GCM"
  }
}
```
