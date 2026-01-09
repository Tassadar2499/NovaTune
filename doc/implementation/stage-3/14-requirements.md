# 14. Requirements

## Requirements Covered

| Requirement | Description |
|-------------|-------------|
| `Req 3.1` | Asynchronous audio processing (metadata extraction, waveform generation) |
| `Req 3.2` | Consume `AudioUploadedEvent` and invoke processing logic |
| `Req 3.3` | Extract metadata via ffprobe, compute duration, persist to RavenDB |
| `Req 3.4` | Transition track to `Ready` or `Failed` |
| `Req 3.5` | Idempotent processing per `TrackId` |
| `NF-1.2` | Health checks for worker readiness |
| `NF-1.4` | Resilience (timeouts, retries, circuit breakers) |
| `NF-2.1` | Horizontal scaling with bounded concurrency |
| `NF-2.4` | Bounded memory usage, streaming IO |
| `NF-4.1` | Structured logging with `CorrelationId` |
| `NF-4.2` | Metrics for consumption lag, success/failure counts |
| `NF-4.3` | Traceability across API → event → worker |
| `NF-6.2` | Optimistic concurrency, monotonic state transitions |

## Open Items

- [ ] Finalize waveform format (WAV vs JSON peaks) based on frontend requirements
- [ ] Define manual retry flow for `Failed` tracks (admin endpoint or requeue)
- [ ] Determine if embedded metadata (title/artist) should auto-populate track fields
- [ ] Evaluate ffmpeg alternatives (e.g., audiowaveform library) for performance
- [ ] Define SLO for "time to ready" (upload → ready latency)
- [ ] Configure alert thresholds for `prod` vs `staging`
