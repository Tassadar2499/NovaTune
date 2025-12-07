# Step 1.3.3: Create Environment Variables Template

> **Parent Task:** [Task 1.3: Docker Compose Infrastructure](overview.md)
> **Status:** Pending
> **Output:** `.env.example`

## Objective

Create a comprehensive environment variables template that documents all configuration options for NovaTune. This file serves as both documentation and a starting point for developer setup.

## Background

Environment variables provide a secure, flexible way to configure applications without hardcoding values. The `.env.example` pattern:

1. Documents all required/optional variables
2. Provides sensible defaults for development
3. Prevents committing secrets to version control
4. Enables different configurations per environment

## Implementation Steps

### 1. Create the Environment Template

Create `.env.example` in the repository root:

```bash
# =============================================================================
# NovaTune Environment Configuration
# =============================================================================
# Copy this file to .env and update values for your environment:
#   cp .env.example .env
#
# IMPORTANT: Never commit .env to version control!
# =============================================================================

# -----------------------------------------------------------------------------
# RavenDB Configuration
# -----------------------------------------------------------------------------
# RavenDB document database connection settings.
# Studio UI available at: http://localhost:8080

RAVENDB_URL=http://localhost:8080
RAVENDB_DATABASE=NovaTune

# Optional: For cluster deployments, comma-separated URLs
# RAVENDB_URLS=http://node1:8080,http://node2:8080,http://node3:8080

# Optional: Certificate path for secure connections
# RAVENDB_CERT_PATH=./certs/ravendb.pfx
# RAVENDB_CERT_PASSWORD=

# -----------------------------------------------------------------------------
# MinIO (S3-Compatible Storage)
# -----------------------------------------------------------------------------
# Object storage for audio files. S3-compatible API.
# Console UI available at: http://localhost:9001

MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
MINIO_USE_SSL=false

# Bucket configuration
MINIO_BUCKET_AUDIO=novatune-audio
MINIO_BUCKET_COVERS=novatune-covers
MINIO_BUCKET_TEMP=novatune-temp

# Presigned URL expiration (in seconds)
MINIO_PRESIGNED_EXPIRY=3600

# -----------------------------------------------------------------------------
# Kafka Configuration
# -----------------------------------------------------------------------------
# Event streaming for analytics and async processing.
# Uses KRaft mode (no ZooKeeper required).

KAFKA_BOOTSTRAP_SERVERS=localhost:9092
KAFKA_GROUP_ID=novatune-api
KAFKA_CLIENT_ID=novatune-producer

# Topic names
KAFKA_TOPIC_AUDIO_UPLOADED=novatune.audio.uploaded
KAFKA_TOPIC_AUDIO_PROCESSED=novatune.audio.processed
KAFKA_TOPIC_ANALYTICS=novatune.analytics.events
KAFKA_TOPIC_USER_ACTIVITY=novatune.user.activity

# Consumer settings
KAFKA_AUTO_OFFSET_RESET=earliest
KAFKA_ENABLE_AUTO_COMMIT=true
KAFKA_SESSION_TIMEOUT_MS=30000

# -----------------------------------------------------------------------------
# RabbitMQ Configuration
# -----------------------------------------------------------------------------
# Message broker for task queues.
# Management UI available at: http://localhost:15672

RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USERNAME=guest
RABBITMQ_PASSWORD=guest
RABBITMQ_VIRTUAL_HOST=/

# Queue names
RABBITMQ_QUEUE_TRANSCODE=novatune.transcode
RABBITMQ_QUEUE_THUMBNAIL=novatune.thumbnail
RABBITMQ_QUEUE_NOTIFICATION=novatune.notification

# Connection settings
RABBITMQ_PREFETCH_COUNT=10
RABBITMQ_RETRY_COUNT=3

# -----------------------------------------------------------------------------
# NCache Configuration
# -----------------------------------------------------------------------------
# Distributed caching for sessions and presigned URLs.
# Management UI available at: http://localhost:8250

NCACHE_SERVER=localhost:9800
NCACHE_CACHE_NAME=novatune-cache

# Cache expiration defaults (in seconds)
NCACHE_SESSION_EXPIRY=1800
NCACHE_URL_CACHE_EXPIRY=3600
NCACHE_METADATA_CACHE_EXPIRY=300

# -----------------------------------------------------------------------------
# JWT Configuration
# -----------------------------------------------------------------------------
# Authentication token settings.
# IMPORTANT: Generate strong keys for production!

JWT_ISSUER=https://novatune.local
JWT_AUDIENCE=novatune-api

# Signing key configuration
# Option 1: Path to PEM key file
JWT_SIGNING_KEY_PATH=./keys/signing.pem
# Option 2: Base64-encoded key (for containerized deployments)
# JWT_SIGNING_KEY_BASE64=

# Token expiration
JWT_ACCESS_TOKEN_EXPIRY_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRY_DAYS=7

# Optional: RSA key pair for asymmetric signing
# JWT_PUBLIC_KEY_PATH=./keys/public.pem
# JWT_PRIVATE_KEY_PATH=./keys/private.pem

# -----------------------------------------------------------------------------
# Password Hashing (Argon2)
# -----------------------------------------------------------------------------
# Password hashing parameters. Adjust based on server capabilities.
# Higher values = more secure but slower.

ARGON2_MEMORY_SIZE=65536
ARGON2_ITERATIONS=4
ARGON2_PARALLELISM=4

# -----------------------------------------------------------------------------
# Audio Processing
# -----------------------------------------------------------------------------
# FFmpeg/FFprobe configuration for audio transcoding.

FFMPEG_PATH=/usr/bin/ffmpeg
FFPROBE_PATH=/usr/bin/ffprobe

# Transcoding settings
AUDIO_MAX_UPLOAD_SIZE_MB=500
AUDIO_ALLOWED_FORMATS=mp3,flac,wav,aac,ogg,m4a
AUDIO_DEFAULT_BITRATE=320k
AUDIO_THUMBNAIL_SIZE=300

# -----------------------------------------------------------------------------
# Application Settings
# -----------------------------------------------------------------------------
# ASP.NET Core configuration.

ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=https://localhost:5001;http://localhost:5000

# Logging
LOG_LEVEL=Information
LOG_FORMAT=json

# CORS (comma-separated origins)
CORS_ORIGINS=http://localhost:3000,http://localhost:5173

# Rate limiting
RATE_LIMIT_REQUESTS_PER_MINUTE=100
RATE_LIMIT_BURST_SIZE=20

# -----------------------------------------------------------------------------
# Observability
# -----------------------------------------------------------------------------
# OpenTelemetry and monitoring configuration.

OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
OTEL_SERVICE_NAME=novatune-api
OTEL_TRACES_SAMPLER=parentbased_traceidratio
OTEL_TRACES_SAMPLER_ARG=0.1

# Aspire Dashboard (when running with Aspire)
ASPIRE_DASHBOARD_URL=http://localhost:18888

# -----------------------------------------------------------------------------
# Feature Flags
# -----------------------------------------------------------------------------
# Toggle features on/off for development/testing.

FEATURE_ANALYTICS_ENABLED=true
FEATURE_TRANSCODING_ENABLED=true
FEATURE_EMAIL_VERIFICATION_REQUIRED=false
```

### 2. Update .gitignore

Ensure `.env` is never committed:

```bash
# Check if .env is in .gitignore
grep -q "^\.env$" .gitignore || echo ".env" >> .gitignore
```

### 3. Create Developer Setup Script

Create `scripts/setup-env.sh`:

```bash
#!/bin/bash
set -e

ENV_FILE=".env"
EXAMPLE_FILE=".env.example"

if [ -f "$ENV_FILE" ]; then
    echo "Warning: $ENV_FILE already exists."
    read -p "Overwrite? (y/N): " confirm
    if [ "$confirm" != "y" ] && [ "$confirm" != "Y" ]; then
        echo "Aborted."
        exit 0
    fi
fi

cp "$EXAMPLE_FILE" "$ENV_FILE"
echo "Created $ENV_FILE from $EXAMPLE_FILE"
echo ""
echo "Next steps:"
echo "1. Review and update values in $ENV_FILE"
echo "2. Generate JWT signing key: ./scripts/generate-keys.sh"
echo "3. Start infrastructure: docker compose up -d"
```

## Variable Categories

### Required Variables

These must be configured for the application to start:

| Variable | Description | Default |
|----------|-------------|---------|
| `RAVENDB_URL` | Database connection URL | `http://localhost:8080` |
| `RAVENDB_DATABASE` | Database name | `NovaTune` |
| `MINIO_ENDPOINT` | Storage endpoint | `localhost:9000` |
| `MINIO_ACCESS_KEY` | Storage access key | `minioadmin` |
| `MINIO_SECRET_KEY` | Storage secret key | `minioadmin` |
| `KAFKA_BOOTSTRAP_SERVERS` | Kafka broker address | `localhost:9092` |
| `JWT_ISSUER` | Token issuer | `https://novatune.local` |

### Security-Sensitive Variables

These should be changed in production:

| Variable | Development Value | Production Guidance |
|----------|-------------------|---------------------|
| `MINIO_ACCESS_KEY` | `minioadmin` | 20+ character random string |
| `MINIO_SECRET_KEY` | `minioadmin` | 40+ character random string |
| `RABBITMQ_PASSWORD` | `guest` | Strong unique password |
| `JWT_SIGNING_KEY_PATH` | Local file | Use secret management (Vault, AWS Secrets) |
| `ARGON2_*` | Development values | Tune for production hardware |

### Optional Variables

These have sensible defaults but can be customized:

| Variable | Default | Purpose |
|----------|---------|---------|
| `LOG_LEVEL` | `Information` | Logging verbosity |
| `RATE_LIMIT_*` | Various | API rate limiting |
| `FEATURE_*` | `true`/`false` | Feature toggles |
| `OTEL_*` | Various | Observability settings |

## Loading Environment Variables

### In Docker Compose

Docker Compose automatically loads `.env` from the current directory:

```yaml
# docker-compose.yml
services:
  api:
    environment:
      - RAVENDB_URL=${RAVENDB_URL}
```

### In ASP.NET Core

Configure in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Load .env file in development
if (builder.Environment.IsDevelopment())
{
    DotNetEnv.Env.Load();
}

// Access variables
var ravenUrl = Environment.GetEnvironmentVariable("RAVENDB_URL");
```

### In Integration Tests

Use `appsettings.Test.json` or override in test setup:

```csharp
Environment.SetEnvironmentVariable("RAVENDB_DATABASE", "NovaTune_Test");
```

## Validation

### Check Environment Loading

```bash
# Start with verbose output
docker compose config

# Verify variable substitution
docker compose config | grep MINIO
```

### Validate Required Variables

Create `scripts/validate-env.sh`:

```bash
#!/bin/bash

required_vars=(
    "RAVENDB_URL"
    "MINIO_ENDPOINT"
    "KAFKA_BOOTSTRAP_SERVERS"
    "JWT_ISSUER"
)

missing=0
for var in "${required_vars[@]}"; do
    if [ -z "${!var}" ]; then
        echo "ERROR: $var is not set"
        missing=1
    fi
done

if [ $missing -eq 1 ]; then
    exit 1
fi

echo "All required variables are set"
```

## Security Considerations

1. **Never commit `.env`** - Contains secrets
2. **Rotate credentials regularly** - Especially in production
3. **Use secret management** - Vault, AWS Secrets Manager, Azure Key Vault
4. **Limit access** - `.env` should be readable only by the application user
5. **Audit changes** - Track who modifies environment configuration

## Navigation

[Previous: Step 1.3.2 - Docker Compose Override](step-1.3.2-docker-compose-override.md) | [Overview](overview.md) | [Next: Step 1.3.4 - Resource Requirements](step-1.3.4-resource-requirements.md)
