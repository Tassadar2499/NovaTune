# Implementation Plan: Redpanda & Garnet Migration

## Goal
Replace Kafka and RabbitMQ with Redpanda for all messaging and swap NCache for Garnet-based caching across docs, infrastructure, and code while keeping functional (FR) and non-functional (NF) requirements intact.

---

## Claude Skills

The following Claude skills are available in `.claude/skills/` to assist with this migration:

| Skill | Directory | Purpose |
|-------|-----------|---------|
| **migration-redpanda-garnet** | `.claude/skills/migration-redpanda-garnet/` | Phase checklists, code patterns for KafkaFlow handlers and Garnet cache |
| **docker-infra** | `.claude/skills/docker-infra/` | Docker Compose commands for Redpanda, Garnet, MinIO, RavenDB |
| **build-and-run** | `.claude/skills/build-and-run/` | .NET/Aspire build, run, test, and format commands |
| **add-api-endpoint** | `.claude/skills/add-api-endpoint/` | Minimal API endpoint patterns with proper conventions |
| **add-entity-field** | `.claude/skills/add-entity-field/` | Add fields to entity/model classes |
| **testing** | `.claude/skills/testing/` | Unit and integration test patterns with Testcontainers |

---

## Decisions from Clarifications
- Message format stays JSON; no Avro or Schema Registry required for this migration (schema version carried in headers).
- RabbitMQ-only semantics (delayed delivery, priority, per-message TTL) are **not** needed; we only migrate simple pub/sub + retries.
- Topics: `audio-events` (3 partitions, 7d retention), `track-deletions` (3 partitions, compaction enabled, 30d max).
- Namespacing: prefix topics and consumer groups with environment (dev/stage/prod); no tenant prefixing.
- Security: TLS + SASL/SCRAM in stage/prod; plaintext allowed in dev. Garnet: no auth in dev; password + TLS in stage/prod.
- Cache durability: Garnet with AOF persistence enabled for session and presigned URL caches (durability required per clarification).
- TTLs/limits: keep existing TTLs and payload limits from NCache (no increases); revisit after burn-in.
- Tests: integration suites use Testcontainers for Redpanda and Garnet; unit tests may stub messaging/cache.
- Rollout: dev → stage → prod with a short dual-run (Kafka + Redpanda) only in stage; RabbitMQ decommissioned immediately after stage cutover.

---

## Assumptions & Scope
- Redpanda runs in Kafka-API compatibility mode; KafkaFlow (built on Confluent.Kafka) provides a cleaner abstraction with middleware, typed handlers, and better DI integration.
- Garnet is consumed via `StackExchange.Redis` client (RESP protocol compatible).
- No production data migration; plan focuses on dev/staging environments and codebase alignment.
- Aspire 9.0+ has community integrations for Redis-compatible stores; Garnet works via `AddRedis()`.
- KafkaFlow chosen over raw Confluent.Kafka for: middleware pipeline, typed message handlers, built-in retry/error handling, and admin dashboard support.

---

## Phase Overview

| Phase | File | Description |
|-------|------|-------------|
| 1 | [phase-1-infrastructure.md](phase-1-infrastructure.md) | Docker Compose changes for Redpanda & Garnet |
| 2 | [phase-2-nuget.md](phase-2-nuget.md) | NuGet package updates |
| 3 | [phase-3-aspire.md](phase-3-aspire.md) | Aspire AppHost configuration |
| 4 | [phase-4-caching.md](phase-4-caching.md) | Caching layer migration (NCache → Garnet) |
| 5 | [phase-5-messaging.md](phase-5-messaging.md) | Messaging layer migration with KafkaFlow |
| 6 | [phase-6-testing.md](phase-6-testing.md) | Testing updates |
| 7 | [phase-7-documentation.md](phase-7-documentation.md) | Documentation updates |
| 8 | [phase-8-verification.md](phase-8-verification.md) | Verification & cleanup |

See also: [risks-and-checklist.md](risks-and-checklist.md)