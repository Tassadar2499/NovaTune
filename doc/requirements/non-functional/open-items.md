# Open Items (TBD)

- Concrete retry/backoff/DLQ semantics for workers and event publication (beyond the timeout/retry budgets above).
- Audit log storage mechanism and tamper-evidence strategy.
- Concrete bulkhead/circuit breaker configuration per dependency and endpoint class.
- Concrete concurrency limits for worker processing and telemetry ingestion.
