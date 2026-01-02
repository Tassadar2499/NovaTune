# Resilience Scaffolding (`NF-1.4`)

Establish baseline resilience patterns (detailed tuning in later stages):

## Tasks

- Add Polly or `Microsoft.Extensions.Http.Resilience` for HTTP clients.
- Configure default timeouts per dependency class (cache: 500ms, DB: 5s, storage: 10s).
- Scaffold circuit breaker policies (can be refined per-endpoint later).
- Add bulkhead policies for concurrent dependency access.
