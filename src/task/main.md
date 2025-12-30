# Implementation Plan: Redpanda & Garnet Migration

This plan has been split into separate files for easier navigation and management.

## Overview

**Goal:** Replace Kafka and RabbitMQ with Redpanda for all messaging and swap NCache for Garnet-based caching across docs, infrastructure, and code while keeping functional (FR) and non-functional (NF) requirements intact.

## Document Structure

| File | Description |
|------|-------------|
| [intro.md](intro.md) | Goal, Claude Skills, Decisions, Assumptions |
| [phase-1-infrastructure.md](phase-1-infrastructure.md) | Docker Compose changes for Redpanda & Garnet |
| [phase-2-nuget.md](phase-2-nuget.md) | NuGet package updates |
| [phase-3-aspire.md](phase-3-aspire.md) | Aspire AppHost configuration |
| [phase-4-caching.md](phase-4-caching.md) | Caching layer migration (NCache → Garnet) |
| [phase-5-messaging.md](phase-5-messaging.md) | Messaging layer migration with KafkaFlow |
| [phase-6-testing.md](phase-6-testing.md) | Testing updates with Testcontainers |
| [phase-7-documentation.md](phase-7-documentation.md) | Documentation updates across doc/ directory |
| [phase-8-verification.md](phase-8-verification.md) | Verification & cleanup |
| [risks-and-checklist.md](risks-and-checklist.md) | Risks, rollback procedures, final checklist |

## Quick Reference

### Claude Skills

Available in `.claude/skills/`:
- **migration-redpanda-garnet** - Phase checklists, code patterns
- **docker-infra** - Docker Compose commands
- **build-and-run** - .NET/Aspire build commands
- **add-api-endpoint** - Minimal API patterns
- **testing** - Test patterns with Testcontainers

### Key Technologies

| Old | New |
|-----|-----|
| Apache Kafka | Redpanda (Kafka-compatible) |
| RabbitMQ | Redpanda (unified messaging) |
| NCache | Garnet (Redis-compatible) |
| Confluent.Kafka | KafkaFlow |
| NCache SDK | StackExchange.Redis |

### Phase Completion

- [ ] Phase 1: Infrastructure
- [ ] Phase 2: NuGet
- [ ] Phase 3: Aspire
- [ ] Phase 4: Caching
- [ ] Phase 5: Messaging
- [ ] Phase 6: Testing
- [ ] Phase 7: Documentation
- [ ] Phase 8: Verification
