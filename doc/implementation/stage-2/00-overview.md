# Stage 2 — Upload Initiation + MinIO Notification Ingestion

**Goal:** Allow direct-to-MinIO uploads with MinIO as the source of truth for completion.

## Overview

```
┌─────────┐  POST /tracks/upload/initiate   ┌─────────────┐
│ Client  │ ───────────────────────────────►│ API Service │
└────┬────┘ ◄─────────────────────────────── └──────┬──────┘
     │       presigned URL + UploadSession          │
     │                                              │ Create UploadSession
     │                                              ▼
     │       PUT (presigned)              ┌─────────────────┐
     │ ──────────────────────────────────►│     MinIO       │
     │                                    └────────┬────────┘
     │                                             │ Bucket notification
     │                                             ▼
     │                                    ┌─────────────────┐
     │                                    │   Redpanda      │
     │                                    │ (minio-events)  │
     │                                    └────────┬────────┘
     │                                             │
     │                                             ▼
     │                                    ┌─────────────────┐
     │                                    │ Upload Ingestor │
     │                                    │    Worker       │
     │                                    └────────┬────────┘
     │                                             │ 1. Validate UploadSession
     │                                             │ 2. Create Track record
     │                                             │ 3. Publish AudioUploadedEvent (via outbox)
     │                                             ▼
     │                                    ┌─────────────────┐
     │                                    │    RavenDB      │
     │                                    └─────────────────┘
```
