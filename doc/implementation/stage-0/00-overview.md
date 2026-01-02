# Stage 0 — Infrastructure & Local Dev Composition

**Goal:** Make local runs mirror production topology early, establishing the dependency graph, observability spine, and configuration validation that all later stages depend on.

## Current State

The codebase already includes:
- Garnet (Redis-compatible cache) via `Aspire.StackExchange.Redis`
- Redpanda (Kafka-compatible) via KafkaFlow with `{prefix}-audio-events` and `{prefix}-track-deletions` topics
- Basic health endpoints (`/health`, `/alive`) in `ServiceDefaults`
  outages
