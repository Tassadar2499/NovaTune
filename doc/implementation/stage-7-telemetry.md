# Stage 7 — Telemetry Ingestion + Analytics Aggregation

**Goal:** Store short-retention analytics and make it visible to Admin.

## API Endpoint

- `POST /telemetry/playback` (or similar) for client-reported play events (`Req 5.4`).
- Rate limit telemetry ingestion (`Req 8.2`, `NF-2.5`).

## Pipeline

- Publish telemetry events to Redpanda (recommended) and aggregate in a worker.
- Store aggregates in RavenDB for admin dashboards (`Req 9.2`).
- Enforce analytics retention (30 days configurable; `NF-6.3`).

## Requirements Covered

- `Req 5.4`
- `Req 9.x`
- `NF-6.3`
- `NF-4.2`
