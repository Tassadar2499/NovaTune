# Project Brief: NovaTune

## Overview
NovaTune is a production-grade, event-driven audio streaming platform built with .NET 9 Aspire orchestration. It enables users to upload, process, manage, and stream audio tracks with playlist support, telemetry, and admin moderation.

## Core Requirements
- **Audio Upload**: Direct-to-MinIO uploads via presigned URLs with server-side session correlation
- **Audio Processing**: Automated metadata extraction (ffprobe) and waveform generation (ffmpeg)
- **Streaming**: Presigned GET URLs with AES-GCM encrypted caching and range request support
- **Track Management**: CRUD with soft-delete (30-day grace period), cursor pagination, search/filter/sort
- **Playlists**: CRUD with ordered track entries, position-based reordering, private/public visibility
- **Telemetry**: Playback event ingestion (start/progress/complete), daily aggregation, analytics
- **Admin & Moderation**: User management, track moderation, analytics dashboards, tamper-evident audit logs
- **Authentication**: JWT + refresh token rotation with Argon2id password hashing

## Target Users
- **Listener**: Upload, manage, and stream audio tracks; create playlists
- **Admin**: Moderate content, manage users, view analytics, review audit logs

## Technical Constraints
- .NET 9 with Aspire orchestration
- RavenDB as sole document database
- Redpanda (Kafka-compatible) for event-driven messaging
- MinIO (S3-compatible) for object storage
- Garnet (Redis-compatible) for distributed caching
- Vue.js 3 + TypeScript frontend (pnpm monorepo)
- All IDs use ULID format (26-character string)
