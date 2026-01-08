# 1. API Endpoint: `POST /tracks/upload/initiate`

## Request Schema

```json
{
  "fileName": "my-track.mp3",        // Required; used for title default
  "mimeType": "audio/mpeg",          // Required; validated against allow-list
  "fileSizeBytes": 15728640,         // Required; validated against max size
  "title": "My Track",               // Optional; defaults to fileName sans extension
  "artist": "Artist Name"            // Optional
}
```

## Response Schema (Success: 200 OK)

```json
{
  "uploadId": "01HXK...",            // ULID
  "trackId": "01HXK...",             // ULID (reserved, Track record not yet created)
  "presignedUrl": "https://...",     // PUT URL; expires in ~15 min
  "expiresAt": "2025-01-08T12:30:00Z",
  "objectKey": "audio/{userId}/{trackId}/{randomSuffix}"  // For reference only
}
```

## Validation Rules (Req 2.2, NF-2.4)

| Field | Rule | Error Code |
|-------|------|------------|
| `mimeType` | Must be in allowed list (configurable) | `UNSUPPORTED_MIME_TYPE` |
| `fileSizeBytes` | ≤ `MaxUploadSizeBytes` (default: 100 MB) | `FILE_TOO_LARGE` |
| `fileSizeBytes` | User quota not exceeded | `QUOTA_EXCEEDED` |
| `fileName` | Non-empty, ≤ 255 chars | `INVALID_FILE_NAME` |

**Supported MIME types** (initial, configurable via `appsettings`):
- `audio/mpeg` (.mp3)
- `audio/mp4` (.m4a)
- `audio/flac` (.flac)
- `audio/wav`, `audio/x-wav` (.wav)
- `audio/ogg` (.ogg)

## Rate Limiting (Req 8.2, NF-2.5)

- Policy: `upload-initiate`
- Default: 10 requests/minute per user
- Response on limit: `429 Too Many Requests` with `Retry-After` header

## Error Responses (RFC 7807)

```json
{
  "type": "https://novatune.dev/errors/quota-exceeded",
  "title": "Storage quota exceeded",
  "status": 400,
  "detail": "You have used 950 MB of your 1 GB storage quota.",
  "instance": "/tracks/upload/initiate",
  "extensions": {
    "usedBytes": 996147200,
    "quotaBytes": 1073741824
  }
}
```
