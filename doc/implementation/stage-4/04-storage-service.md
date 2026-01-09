# 4. Storage Service Extension

Add presigned GET URL generation to `IStorageService`:

```csharp
/// <summary>
/// Generates a presigned GET URL for streaming.
/// </summary>
/// <param name="objectKey">The storage object key.</param>
/// <param name="expiry">URL expiry duration.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>Presigned URL and expiry timestamp.</returns>
Task<PresignedDownloadResult> GeneratePresignedDownloadUrlAsync(
    string objectKey,
    TimeSpan expiry,
    CancellationToken ct = default);

public record PresignedDownloadResult(string Url, DateTimeOffset ExpiresAt);
```

## Implementation

```csharp
public async Task<PresignedDownloadResult> GeneratePresignedDownloadUrlAsync(
    string objectKey,
    TimeSpan expiry,
    CancellationToken ct = default)
{
    return await _presignPipeline.ExecuteAsync(async token =>
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(_audioBucket)
            .WithObject(objectKey)
            .WithExpiry((int)expiry.TotalSeconds);

        var url = await _minioClient.PresignedGetObjectAsync(args);
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);

        _logger.LogDebug(
            "Generated presigned download URL for {ObjectKey}, expires at {ExpiresAt}",
            objectKey, expiresAt);

        return new PresignedDownloadResult(url, expiresAt);
    }, ct);
}
```
