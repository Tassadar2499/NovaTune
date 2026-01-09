# 12. Implementation Tasks

## API Service

- [ ] Add `IStreamingService` interface and `StreamingService` implementation
- [ ] Add `IEncryptedCacheService` interface and `EncryptedCacheService` implementation
- [ ] Add `ICacheEncryptionProvider` interface with AES-256-GCM implementation
- [ ] Add `GeneratePresignedDownloadUrlAsync` to `IStorageService`
- [ ] Add `POST /tracks/{trackId}/stream` endpoint
- [ ] Add `StreamingOptions` configuration class
- [ ] Add rate limiting policy `stream-url`
- [ ] Add streaming metrics to `NovaTuneMetrics`

## Infrastructure

- [ ] Add cache encryption key configuration to AppHost
- [ ] Configure MinIO CORS for range requests (if cross-origin needed)
- [ ] Add `CacheEncryption` configuration section

## Event Handlers

- [ ] Update `TrackDeletedHandler` to invalidate streaming cache
- [ ] Add cache invalidation to `AuthService.LogoutAllAsync()`

## Testing

- [ ] Unit tests for `StreamingService`
- [ ] Unit tests for `EncryptedCacheService`
- [ ] Integration tests for stream URL flow
- [ ] Integration tests for cache invalidation
