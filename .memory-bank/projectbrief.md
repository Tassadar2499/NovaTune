# Project Brief: NovaTune

## Overview
NovaTune is a self-hosted audio platform built around a .NET 9 backend and a Vue 3 frontend workspace. The repository contains a production-oriented backend for authentication, uploads, track processing, streaming, playlists, telemetry, and admin moderation, plus two SPAs:
- `player`: listener-facing library, playback, playlists, and upload flows
- `admin`: moderation, analytics, and audit workflows

## Repository Scope
- `src/NovaTuneApp/`: .NET solution and Aspire orchestration
- `src/NovaTuneClient/`: pnpm monorepo for the Vue apps and shared packages
- `src/unit_tests/`: xUnit unit tests
- `src/integration_tests/`: Aspire-backed integration tests
- `doc/`: requirements, stage plans, frontend planning, and diagrams

## Core Product Capabilities
- JWT auth with refresh token rotation and role separation
- Direct-to-MinIO upload initiation with correlated upload sessions
- Event-driven ingestion and background audio processing
- Track library CRUD with soft delete and lifecycle cleanup
- Presigned streaming URLs with encrypted cache storage
- Playlist CRUD with ordered track membership
- Playback telemetry ingestion and aggregate analytics
- Admin user management, track moderation, and audit logging

## Major Architectural Decisions
- RavenDB is the system of record for domain data and aggregates
- Redpanda/Kafka is used for asynchronous workflows outside Testing
- MinIO stores audio assets and processing artifacts
- Garnet/Redis is used for caching, including encrypted stream URL cache entries
- .NET Aspire composes the local stack and switches behavior by environment
- Vue apps run under Vite in Development and are copied into `NovaTuneApp.Web/wwwroot` for Release builds

## Working Assumptions
- The backend is substantially implemented across API + workers
- The frontend has concrete route/page/store structure, but test coverage and final production hardening are still thinner than the backend
- `.memory-bank` should track the codebase as it exists, not the earlier planning-only state
