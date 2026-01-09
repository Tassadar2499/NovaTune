# 10. Resilience (NF-1.4)

## Timeouts

| Operation | Timeout | Notes |
|-----------|---------|-------|
| MinIO download | 5 min | Large files up to 500 MB |
| ffprobe execution | 30s | Should complete quickly |
| ffmpeg waveform | 2 min | CPU-bound |
| RavenDB read | 5s | |
| RavenDB write | 10s | |
| Total processing | 10 min | Hard limit per track |

## Circuit Breaker

- **MinIO**: Open after 5 consecutive failures, half-open after 30s
- **RavenDB**: Open after 5 consecutive failures, half-open after 30s

## Resource Limits

| Resource | Limit | Notes |
|----------|-------|-------|
| Temp disk space | 2 GB | Fail fast if exceeded |
| Memory per process | 512 MB soft limit | Streaming IO prevents spikes |
| Concurrent ffmpeg | = `MaxConcurrency` | Bound by consumer concurrency |

## Graceful Shutdown

1. Stop accepting new messages
2. Wait for in-flight processing to complete (timeout: 60s)
3. Commit final offsets
4. Clean up temp files
5. Exit
