# Requirements Covered

## Functional Requirements

- `Req 5.1` — Listener can request playback for owned tracks
- `Req 5.2` — Short-lived presigned GET URL with cache reuse
- `Req 5.3` — Range request support for byte-range playback
- `Req 10.2` — Cache presigned URLs by user+track with invalidation
- `Req 10.3` — Encrypted cache entries for presigned URLs
- `Req 10.4` — Configurable cache behavior (key prefix, TTLs)

## Non-Functional Requirements

- `NF-2.3` — Efficient caching reduces MinIO presign calls
- `NF-3.3` — Short-lived, scoped presigned URLs
- `NF-6.1` — No streaming for deleted tracks

---

# Open Items

- [ ] Determine exact presign TTL for production (60s vs 120s)
- [ ] Finalize KMS integration for production encryption keys
- [ ] Define CORS policy for cross-origin audio players
- [ ] Determine if waveform data should also use presigned URLs
- [ ] Consider adding track-level access sharing (future scope)
