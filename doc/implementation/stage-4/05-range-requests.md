# 5. Range Requests (Req 5.3)

## MinIO Configuration

MinIO natively supports HTTP Range requests. Ensure the bucket and presigned URLs allow byte-range playback:

### Bucket CORS Configuration

If cross-origin playback is needed:

```json
{
  "CORSRules": [{
    "AllowedOrigins": ["https://app.novatune.dev"],
    "AllowedMethods": ["GET", "HEAD"],
    "AllowedHeaders": ["Range", "Content-Range"],
    "ExposeHeaders": ["Accept-Ranges", "Content-Range", "Content-Length"],
    "MaxAgeSeconds": 3600
  }]
}
```

### Client Behavior

- Clients send `Range: bytes=0-1048575` header
- MinIO responds with `206 Partial Content` and `Content-Range` header
- Presigned URLs include all necessary auth for range requests

## Response Headers (from MinIO)

```
HTTP/1.1 206 Partial Content
Accept-Ranges: bytes
Content-Range: bytes 0-1048575/15728640
Content-Length: 1048576
Content-Type: audio/mpeg
```
