# Observability Baseline (`NF-4.x`)

Establish the instrumentation spine before feature work:

| Concern             | Implementation                                                       |
|---------------------|----------------------------------------------------------------------|
| Structured logging  | Serilog with JSON output; `CorrelationId` enricher                   |
| Distributed tracing | OpenTelemetry via Aspire; `traceparent` propagation                  |
| Metrics             | Request rate/latency/error via OpenTelemetry                         |
| Redaction           | Never log passwords, tokens, presigned URLs, object keys (`NF-4.5`)  |

## Tasks

- Configure Serilog with JSON formatter and correlation enrichment.
- Add OpenTelemetry exporters (Aspire dashboard for local; configurable for prod).
- Implement log redaction middleware or destructuring policy.
- Verify `X-Correlation-Id` header propagation from gateway.
