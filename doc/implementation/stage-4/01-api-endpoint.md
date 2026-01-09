# 1. API Endpoint: `POST /tracks/{trackId}/stream`

## Request

- **Method:** `POST`
- **Path:** `/tracks/{trackId}/stream`
- **Authentication:** Required (Bearer token)
- **Authorization:** Listener role; must own the track or have explicit access

## Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `trackId` | string | ULID identifier for the track |

## Response Schema (Success: 200 OK)

```json
{
  "streamUrl": "https://minio.example.com/...",
  "expiresAt": "2025-01-08T12:32:00Z",
  "contentType": "audio/mpeg",
  "fileSizeBytes": 15728640,
  "supportsRangeRequests": true
}
```

## Validation Rules (Req 5.1, NF-6.1)

| Check | Rule | Error |
|-------|------|-------|
| Track exists | Track document must exist in RavenDB | `404 Not Found` |
| Ownership | `Track.UserId` must match authenticated user | `403 Forbidden` |
| Track status | `Track.Status` must be `Ready` | `409 Conflict` |
| User status | User must be `Active` | `403 Forbidden` |

## Rate Limiting (Req 8.2, NF-2.5)

- Policy: `stream-url`
- Default: 60 requests/minute per user
- Response on limit: `429 Too Many Requests` with `Retry-After` header

## Error Responses (RFC 7807)

```json
{
  "type": "https://novatune.dev/errors/track-not-ready",
  "title": "Track not ready for streaming",
  "status": 409,
  "detail": "Track is currently processing. Please wait until processing completes.",
  "instance": "/tracks/01HXK.../stream",
  "extensions": {
    "trackId": "01HXK...",
    "currentStatus": "Processing"
  }
}
```

| Status | Type | Condition |
|--------|------|-----------|
| `400` | `invalid-track-id` | Malformed ULID |
| `401` | `unauthorized` | Missing or invalid authentication |
| `403` | `forbidden` | User does not own track or is suspended |
| `404` | `track-not-found` | Track does not exist |
| `409` | `track-not-ready` | Track status is not `Ready` |
| `429` | `rate-limit-exceeded` | Rate limit exceeded |
| `503` | `service-unavailable` | MinIO or cache unavailable |
