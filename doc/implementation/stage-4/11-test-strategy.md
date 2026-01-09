# 11. Test Strategy

## Unit Tests

- `StreamingService`: Cache hit/miss scenarios
- `StreamingService`: TTL calculation
- `EncryptedCacheService`: Encrypt/decrypt round-trip
- `EncryptedCacheService`: Key version handling
- Cache key generation
- Validation logic in endpoint

## Integration Tests

- End-to-end stream URL flow (cache miss → presign → cache)
- Cache invalidation on track deletion
- Cache invalidation on logout
- Rate limiting enforcement
- Range request verification (client → MinIO)
