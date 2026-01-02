# Functional Requirements — Open Questions

This file captures clarifying questions raised while reviewing `doc/requirements/functional/main.md`.

## Cross-cutting

1. **Identifiers**: should external/API identifiers for `UserId`/`TrackId` be `Guid`, Raven-style string IDs (e.g., `tracks/1`), or something else (e.g., ULID)? How should events and API payloads align?
A: Use ULID
2. **Multi-environment naming**: what is the authoritative mapping for `{env}` in topic names (e.g., `dev`, `staging`, `prod`), and should it be required/configured per deployment?
A: Use `{env}` as the authoritative mapping for topic names, and it should be required/configured per deployment.
3. **Consistency model**: are clients expected to see eventual consistency (e.g., track appears immediately as `Processing`, then later becomes `Ready`), or are there flows that must be strongly consistent?
A: Clients should see eventual consistency.
4. **Error contract**: should the API use a standard error shape (e.g., RFC 7807 problem details), and what error codes are expected for common failures (auth, validation, not found, forbidden)?
A: Use RFC 7807 problem details.
5. **Rate limits / abuse**: do we need explicit rate limits for login, upload initiation, playback URL issuance, and telemetry ingestion?
A: Yes, Use explicit rate limits for login, upload initiation, playback URL issuance, and telemetry ingestion.

## Authentication & Authorization (Req 1.x)

1. **Password policy**: required minimum length/complexity, and should breached-password checks be in scope?
A: No required minimum length/complexity,
2. **Password hashing**: preferred algorithm/parameters (Argon2id/bcrypt/PBKDF2), and do we need per-user salt + global pepper?
A: Prefer Argon2id/bcrypt
3. **Email verification**: is email confirmation required before `Status=Active`, and what is the expected flow for resending/expiry?
A: No email confirmation required before `Status=Active`
4. **Refresh tokens**: rotation strategy (one-time use vs reusable), TTLs, max concurrent sessions/devices per user, and whether refresh tokens are stored hashed in RavenDB vs cached in Garnet.
A: Use one-time use rotation strategy, TTLs of 1 hour, max 5 concurrent sessions/devices per user, and refresh tokens are stored hashed in RavenDB.
5. **Revocation semantics (Req 1.5)**: should logout revoke only the current session or all sessions? On password change/admin disable, should all sessions be revoked immediately?
A: Logout revoke only the current session,
6. **Roles/scopes (Req 1.4)**: how are Admins represented (separate user type vs role claim), and what claim(s) should be used for authorization?
A: Admins should be represented as a separate user type, and use `admin` role claim for authorization.
7. **`PendingDeletion` policy (Req 1.3)**: which operations remain allowed (login, streaming, metadata edits, delete requests), and what cleanup timeline is expected?
A: Only login and streaming are allowed, and cleanup timeline is expected within 30 days.

## Upload (Req 2.x)

1. **Upload mechanics (Req 2.1/2.3)**: is the intended approach direct-to-MinIO via presigned PUT/POST, a proxied upload through the API, or multipart/chunked uploads?
A: Use direct-to-MinIO via presigned PUT/POST.
2. **Upload completion**: if uploads are direct-to-MinIO, what is the source of truth that the upload completed successfully (client callback to API, MinIO event notification, periodic reconciliation)?
A: Use MinIO event notification.
3. **Supported formats (Req 2.2)**: what formats/codecs are supported initially, and should validation be based on MIME type, file extension, magic bytes, or a decoding attempt?
A: Use MIME type,
4. **Limits**: max upload size, max duration, and any per-user quotas (storage, track count).
A: Use max upload size, max duration, and any per-user quotas (storage, track count).
5. **Deduplication**: should the system dedupe uploads via checksum (Req 2.6 includes `Checksum` as optional on Track), and if so when/how is checksum computed?
A: Use checksum computation after upload completion.
6. **ObjectKey scheme (Req 2.3)**: should `ObjectKey` be guess-resistant and user-scoped (e.g., include user ID), and do we need bucket-per-env or bucket-per-user?
A: Use guess-resistant and user-scoped ObjectKey.
7. **Track record timing (Req 2.4/2.5)**: is the track metadata record created before the upload (to reserve an ID/object key) or after the upload succeeds?
A: Use track metadata record created after the upload succeeds.
8. **Event emission contract (Req 2.6)**: should `AudioUploadedEvent` be emitted exactly-once, at-least-once, or best-effort? What are the expected retry/outbox semantics?
A: Use exactly-once.

## Processing / Workers (Req 3.x)

1. **Retry policy**: how many processing retries, what backoff strategy, and do we need a DLQ/topic for poison messages?
A: Use 3 retries,
2. **Idempotency definition (Req 3.5)**: what should be considered safe to repeat (e.g., overwrite `AudioMetadata`, regenerate waveform), and what should be guarded (e.g., avoid re-transitioning `Ready` back to `Processing`)?
A: Safe to repeat: `AudioMetadata`, Regenerate waveform. Safe to guard: avoid re-transitioning `Ready` back to `Processing`. 
3. **Failure classification (Req 3.4)**: what constitutes an “unrecoverable processing error” vs transient (network, object missing, decoder errors)?
A: Unrecoverable processing error.
4. **Waveform output**: where should waveform artifacts live (RavenDB document, MinIO sidecar object), and what format is desired?
A: Waveform artifacts should live in MinIO sidecar object, and use WAV format.
5. **Concurrency limits**: should processing be limited per user or globally (to protect MinIO/CPU)?
A: Processing should be limited globally.% A simple, self-contained CV (no external .cls required).
   \documentclass[10pt]{article}

\usepackage[T1]{fontenc}
\usepackage[utf8]{inputenc}
\usepackage[left=0.55in,top=0.55in,right=0.55in,bottom=0.55in]{geometry}
\usepackage[hidelinks]{hyperref}
\usepackage{tabularx}
\usepackage{enumitem}
\usepackage{xcolor}

\setlength{\parindent}{0pt}
\setlist[itemize]{leftmargin=*,nosep}
\setlength{\tabcolsep}{0pt}

\newcommand{\cvname}[1]{\textbf{\LARGE #1}\par}
\newcommand{\cvline}[1]{#1\par}
\newcommand{\cvsection}[1]{\vspace{0.85\baselineskip}\textbf{\large #1}\par\vspace{0.15\baselineskip}\hrule\vspace{0.5\baselineskip}}
\newcommand{\cvrole}[3]{\textbf{#1}\hfill #2\par\textit{#3}\par}

\begin{document}

\cvname{Roman Markevich}
\cvline{\textit{C\# / ASP.NET Core Developer \quad\textbar\quad 6+ years experience}}
\cvline{Limassol, Cyprus}
\cvline{Mobile: +44 7715 450685 \quad\textbar\quad Email: \href{mailto:markevich.roma@gmail.com}{markevich.roma@gmail.com}}

\cvsection{Summary}
C\# / ASP.NET Core developer with 6+ years of experience building scalable, high-performance web applications.
Worked in remote and on-site teams (3--20 members), delivering software from concept to production across requirements, architecture, CI/CD, testing, and deployment.
Domain experience includes reporting systems, telephony, document management, marketplaces, CRM platforms, and distributed printing solutions.

\cvsection{Skills}
\small
\begin{itemize}
\item \textbf{Languages:} C\#, Python, Bash, PowerShell
\item \textbf{.NET:} .NET 7--9, ASP.NET Core, Entity Framework Core, Dapper, Blazor, SignalR, Hangfire
\item \textbf{Testing:} xUnit, TestContainers, Verify, Fluent Assertions; functional/integration testing
\item \textbf{Datastores:} MS SQL Server, PostgreSQL, MongoDB, ClickHouse, Redis
\item \textbf{Messaging \& APIs:} Apache Kafka, RabbitMQ, MassTransit; HTTP/REST, gRPC/Protobuf, WebSockets, SNMP
\item \textbf{Cloud:} AWS, Azure
\item \textbf{Infra/Observability:} Kubernetes, Docker/Docker Compose, Nginx, Linux, Windows Server; ELK, Grafana, Prometheus, Graylog, Jaeger; GitLab CI, GitHub Actions; etcd, Vault
\item \textbf{AI tools:} Claude Code, Codex CLI, Cursor
\item \textbf{Architecture:} BPMN, UML, C4
\end{itemize}
\normalsize

\cvsection{Experience}
\cvline{\small Approximate tenure in years (as of Jan 2026).}
\normalsize
\cvrole{Gold Apple}{Sep 2023--Present (2.4 yrs)}{C\# / ASP.NET Core Developer --- Marketplace and online store}
\begin{itemize}
\item Implemented an Outbox pattern for MongoDB and Apache Kafka and integrated it into the open-source Kafka Flow library.
\item Designed a unit testing approach eliminating mocks by relying on standard DI containers.
\item Introduced a specification-driven development process using BDD principles and functional tests for business logic.
\item Proposed and implemented batch message consumption in Kafka, reducing topic lag by 3--4$\times$.
\item \textbf{Technologies:} C\#, .NET 9; ASP.NET Core; MongoDB; Elasticsearch, Logstash, Kibana; gRPC; Redis; in-memory cache
\end{itemize}

\cvrole{Katusha IT}{Feb 2022--Sep 2023 (1.7 yrs)}{C\# / ASP.NET Core Developer --- Distributed printing systems}
\begin{itemize}
\item Designed and supported migration from a legacy system to a microservices architecture deployed on Kubernetes, focusing on fault tolerance and scalability.
\item Built CI/CD pipelines from scratch with Bash and PowerShell for automated builds and deployments.
\item Produced architectural diagrams and collaborated with the lead architect on system design decisions.
\item \textbf{Technologies:} C\#, .NET 8; ASP.NET Core; Entity Framework Core; PostgreSQL; ClickHouse; RabbitMQ; MassTransit; Kubernetes
\end{itemize}

\cvrole{Ozon Tech}{Feb 2019--Feb 2022 (3.1 yrs)}{C\# / ASP.NET Core Developer --- Marketplace and online store}
\begin{itemize}
\item Improved load testing by building tools for dynamic test-data generation.
\item Built a custom integration testing framework using PostgreSQL in Docker.
\item Decomposed a monolith into microservices using the Strangler Pattern for smooth migration.
\item Implemented a Kafka consumer redirection mechanism for version-specific testing in staging environments.
\item Designed a PostgreSQL sharding migration plan optimizing hot/cold storage.
\item Implemented Transactional Outbox messaging for Kafka topics.
\item Proposed and delivered a microservice split, improving read response time by 2.5$\times$ through enhanced scalability.
\item \textbf{Technologies:} C\#, .NET 7; ASP.NET Core; Entity Framework Core; PostgreSQL; Apache Kafka; gRPC; etcd; Vault
\end{itemize}

\cvrole{Directum RX}{Jan 2018--Jan 2019 (1.1 yrs)}{C\# Developer --- Electronic document management system}

\cvsection{Education}
\cvrole{Novosibirsk State University}{2017--2024}{Math and Computer Science / Software Engineering}
\begin{itemize}
\item Bachelor's Degree --- Math and Computer Science (2017--2021)
\item Master's Degree --- Software Engineering (2022--2024)
\end{itemize}

\cvsection{Personal}
In free time, I develop pet projects and contribute to open-source. Recent work: implemented an Outbox pattern for MongoDB in the Kafka Flow library.

\end{document}


## Storage, Presigned URLs, Lifecycle (Req 4.x / Req 10.x)

1. **TTL values (Req 4.2/Req 10.3)**: expected TTLs for presigned upload URLs, presigned streaming URLs, refresh tokens, and revocation flags.
2. **Cache keying (Req 10.2)**: is user+track sufficient, or do we need to include MIME/variant/bitrate/format version in the key?
3. **Security for cached URLs**: is it acceptable to cache full presigned URLs in Garnet, or should they be encrypted at rest / avoided in favor of caching inputs and regenerating?
4. **Invalidation triggers (Req 4.4/Req 10.2)**: besides track deletion, should cached URLs be invalidated on logout, password change, user disable, and permission changes?
5. **Deletion grace period (Req 4.4)**: how long is the grace period, and can a user undo/restore a deletion during that window?

## Streaming & Telemetry (Req 5.x)

1. **Streaming path**: is streaming always direct-from-MinIO via presigned GET, or will the API ever proxy streaming (e.g., for DRM, watermarking, analytics)?
2. **Range requests (Req 5.3)**: any specific client compatibility requirements (iOS/Android/web) that imply constraints on MinIO configuration or headers?
3. **Telemetry capture (Req 5.4)**: since the backend may not see streamed bytes, what is the desired telemetry mechanism (client-reported events vs storage access logs), and what is the minimum schema?
4. **Privacy/retention**: analytics retention period and any privacy constraints (e.g., opt-out, deletion on account removal).

## Track Management (Req 6.x)

1. **Sorting/filtering**: required sort orders (recent, title, artist) and filter semantics (case-insensitive search, partial matches, status filters).
2. **Editable fields (Req 6.2)**: confirm allowed updates (Title/Artist only?), and whether editing is allowed while `Status=Processing`.
3. **Deletion model (Req 6.3/Req 4.4)**: is deletion soft-delete (status change) vs hard-delete of the RavenDB record, and should deletes be idempotent?
4. **Track sharing**: is there any notion of sharing tracks across users, or strictly per-user ownership only?

## Playlists (Req 7.x)

1. **Playlist constraints**: max playlists per user, max tracks per playlist, duplicate tracks allowed or not.
2. **Ordering semantics**: do we require stable ordering with explicit positions, and how should concurrent edits be handled?
3. **Future sharing**: should the model anticipate playlist sharing/collaboration, or keep it strictly private for now?

## Analytics, Events, Admin (Req 9.x / Req 11.x)

1. **Event format**: JSON vs Avro/Protobuf, and is there a schema registry/versioning system expected beyond a `SchemaVersion` field?
2. **Topic partitioning/keying**: what key should be used for partitioning (TrackId, UserId), and what ordering guarantees are required?
3. **CorrelationId propagation (Req 9.3)**: where does `CorrelationId` originate (API gateway, client, backend), and should it be required on inbound requests?
4. **Admin auditing**: do admin actions (disable user, delete track) require audit logs and/or reason codes?
5. **Moderation semantics**: difference between “delete”, “moderate”, and “disable”, and how should these affect streaming URL issuance and processing workers?
