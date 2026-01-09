# 10. Security Considerations

## URL Security (NF-3.3)

- Presigned URLs are short-lived (60-120 seconds in production)
- URLs are user+track scoped (cannot access other users' tracks)
- URLs are encrypted in cache (AES-256-GCM)
- Object keys are guess-resistant (contain random suffix)

## Access Control

- Only track owners can request streaming URLs
- Deleted tracks (`Status=Deleted`) cannot be streamed
- Suspended users cannot request streaming URLs
- Rate limiting prevents abuse

## Audit Trail

Stream URL requests are logged with:
- User ID
- Track ID
- Correlation ID
- Timestamp
- Success/failure status
