# Stage 4 — Streaming URL Issuance + Caching

**Goal:** Allow streaming via short-lived presigned GET without proxying bytes.

## API Endpoint

- `POST /tracks/{trackId}/stream` issues presigned GET URL (`Req 5.1`, `Req 5.2`).
- Enforce ownership and status checks (no issuance for `Deleted` or not permitted; `NF-6.1`).

## Cache Behavior

- Cache presigned URLs in Garnet by user+track (`Req 10.2`).
- TTL slightly shorter than presign expiry (`NF-3.3`).
- Encrypt cached values (`Req 10.3`).

## Range Requests

- Ensure MinIO presign and bucket/object headers allow byte-range playback (`Req 5.3`).

## Requirements Covered

- `Req 5.x`
- `Req 10.x`
- `NF-2.3`
- `NF-3.3`
- `NF-6.1`
