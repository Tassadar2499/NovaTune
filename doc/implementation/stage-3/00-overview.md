# Stage 3 — Post-Upload Audio Processing Worker (ffprobe/ffmpeg)

**Goal:** Process uploaded tracks asynchronously, extract metadata, generate waveforms, and transition to `Ready`.

## Overview

```
┌─────────────────┐                           ┌─────────────────┐
│    RavenDB      │                           │     MinIO       │
│  (Track record) │                           │  (audio files)  │
└────────┬────────┘                           └────────┬────────┘
         │                                             │
         │ Update Track                                │ Fetch audio
         │ (metadata + status)                         │ Store waveform
         │                                             │
         ▼                                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Audio Processing Worker                       │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────────────┐  │
│  │   ffprobe   │───►│  Metadata   │───►│  Waveform Generator │  │
│  │  (extract)  │    │  Validator  │    │      (ffmpeg)       │  │
│  └─────────────┘    └─────────────┘    └─────────────────────┘  │
└────────────────────────────────────────────────────────────────┬┘
                                                                 │
                          ▲                                      │
                          │ Consume AudioUploadedEvent           │
                          │                                      │
┌─────────────────────────┴───────────────────────────────────────┘
│                     Redpanda
│                 ({env}-audio-events)
└─────────────────────────────────────────────────────────────────┘
```

## Document Index

| # | Document | Description |
|---|----------|-------------|
| 01 | [Event Consumption](01-event-consumption.md) | Kafka topic config, event schema, consumer settings |
| 02 | [Processing Pipeline](02-processing-pipeline.md) | Worker project structure and processing flow |
| 03 | [Metadata Extraction](03-metadata-extraction.md) | ffprobe integration, AudioMetadata schema |
| 04 | [Waveform Generation](04-waveform-generation.md) | ffmpeg waveform creation and storage |
| 05 | [Track Document Updates](05-track-updates.md) | Track schema, status transitions, concurrency |
| 06 | [Error Handling](06-error-handling.md) | Error classification, DLQ configuration |
| 07 | [Idempotency](07-idempotency.md) | Replay guarantees and scenarios |
| 08 | [Health Checks](08-health-checks.md) | Readiness requirements and endpoints |
| 09 | [Observability](09-observability.md) | Logging, metrics, tracing, SLOs |
| 10 | [Resilience](10-resilience.md) | Timeouts, circuit breakers, resource limits |
| 11 | [Configuration](11-configuration.md) | appsettings.json reference |
| 12 | [Test Strategy](12-test-strategy.md) | Unit, integration, and load tests |
| 13 | [Implementation Tasks](13-implementation-tasks.md) | Task checklist by category |
| 14 | [Requirements](14-requirements.md) | Requirements covered and open items |
