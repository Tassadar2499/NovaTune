# Step 1.3.2: Create Docker Compose Override

> **Parent Task:** [Task 1.3: Docker Compose Infrastructure](overview.md)
> **Status:** Completed
> **Output:** `docker-compose.override.yml`

## Objective

Create a Docker Compose override file for development-specific configurations. This separates production-ready defaults from development conveniences, following Docker Compose best practices.

## Background

Docker Compose automatically merges `docker-compose.yml` with `docker-compose.override.yml` when running `docker compose up`. This pattern allows:

- Base configuration in `docker-compose.yml` (committed to repo)
- Development overrides in `docker-compose.override.yml` (may vary per developer)
- Production overrides in `docker-compose.prod.yml` (used with `-f` flag)

## Implementation Steps

### 1. Create the Override File

Create `docker-compose.override.yml` in the repository root:

```yaml
version: '3.8'

# =============================================================================
# Development-specific overrides for local development
# This file is automatically merged with docker-compose.yml
# =============================================================================

services:
  # ---------------------------------------------------------------------------
  # RavenDB Development Settings
  # ---------------------------------------------------------------------------
  ravendb:
    environment:
      # License file path (mount your license for production features)
      - RAVEN_License_Path=/opt/RavenDB/Server/license.json
      # Enable detailed logging for debugging
      - RAVEN_Logs_Mode=Information
    # Uncomment to mount a license file for development:
    # volumes:
    #   - ./config/ravendb-license.json:/opt/RavenDB/Server/license.json:ro

  # ---------------------------------------------------------------------------
  # MinIO Development Settings
  # ---------------------------------------------------------------------------
  minio:
    environment:
      # Allow environment variable override for credentials
      - MINIO_ROOT_USER=${MINIO_ACCESS_KEY:-minioadmin}
      - MINIO_ROOT_PASSWORD=${MINIO_SECRET_KEY:-minioadmin}
      # Enable browser console access
      - MINIO_BROWSER=on
    # Create default bucket on startup
    entrypoint: >
      /bin/sh -c "
      mkdir -p /data/novatune-dev-audio;
      minio server /data --console-address ':9001';
      "

  # ---------------------------------------------------------------------------
  # Kafka Development Settings
  # ---------------------------------------------------------------------------
  kafka:
    environment:
      # Auto-create topics for development convenience
      - KAFKA_AUTO_CREATE_TOPICS_ENABLE=true
      # Faster log cleanup for development
      - KAFKA_LOG_RETENTION_HOURS=24
      - KAFKA_LOG_RETENTION_BYTES=1073741824
    deploy:
      resources:
        limits:
          memory: 1G
        reservations:
          memory: 512M

  # ---------------------------------------------------------------------------
  # RabbitMQ Development Settings
  # ---------------------------------------------------------------------------
  rabbitmq:
    environment:
      # Enable all feature flags for testing
      - RABBITMQ_FEATURE_FLAGS=all
    deploy:
      resources:
        limits:
          memory: 512M
        reservations:
          memory: 256M

  # ---------------------------------------------------------------------------
  # NCache Development Settings
  # ---------------------------------------------------------------------------
  ncache:
    environment:
      # Development mode with relaxed settings
      - NCACHE_LICENSE_TYPE=DEV
    deploy:
      resources:
        limits:
          memory: 512M
        reservations:
          memory: 256M

  # ---------------------------------------------------------------------------
  # Optional: Add development-only services
  # ---------------------------------------------------------------------------

  # Kafka UI for topic inspection
  kafka-ui:
    image: provectuslabs/kafka-ui:latest
    container_name: novatune-kafka-ui
    ports:
      - "8081:8080"
    environment:
      - KAFKA_CLUSTERS_0_NAME=novatune-local
      - KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS=kafka:9092
    depends_on:
      - kafka
    profiles:
      - debug
    networks:
      - novatune-network

  # Mailhog for email testing (useful for auth flows)
  mailhog:
    image: mailhog/mailhog:latest
    container_name: novatune-mailhog
    ports:
      - "1025:1025"   # SMTP
      - "8025:8025"   # Web UI
    profiles:
      - debug
    networks:
      - novatune-network
```

## Configuration Details

### Resource Limits

Development resource limits prevent containers from consuming all system memory:

| Service | Memory Limit | Memory Reserved |
|---------|--------------|-----------------|
| Kafka | 1 GB | 512 MB |
| RabbitMQ | 512 MB | 256 MB |
| NCache | 512 MB | 256 MB |

### Environment Variable Substitution

The override file supports `.env` file variables with defaults:

```yaml
environment:
  - MINIO_ROOT_USER=${MINIO_ACCESS_KEY:-minioadmin}
```

This means:
1. Use `MINIO_ACCESS_KEY` from `.env` if defined
2. Fall back to `minioadmin` if not defined

### MinIO Auto-Bucket Creation

The custom entrypoint creates the default bucket automatically:

```yaml
entrypoint: >
  /bin/sh -c "
  mkdir -p /data/novatune-dev-audio;
  minio server /data --console-address ':9001';
  "
```

This eliminates manual bucket creation during first-time setup.

### Docker Compose Profiles

Debug services use profiles to avoid starting by default:

```yaml
profiles:
  - debug
```

**Start with debug services:**
```bash
docker compose --profile debug up -d
```

**Start without debug services (default):**
```bash
docker compose up -d
```

## Debug Services

### Kafka UI

Visual interface for Kafka topic management:

- **URL:** http://localhost:8081
- **Features:**
  - View topics and partitions
  - Browse messages
  - Monitor consumer groups
  - Create/delete topics

### Mailhog

Email testing server for development:

- **SMTP Port:** 1025 (configure app to send here)
- **Web UI:** http://localhost:8025
- **Features:**
  - Catch all outgoing emails
  - View email content and headers
  - API access for testing

## Usage Patterns

### Developer-Specific Overrides

Create a local override that's not committed:

```bash
# Add to .gitignore if not present
echo "docker-compose.local.yml" >> .gitignore
```

Create `docker-compose.local.yml`:

```yaml
version: '3.8'

services:
  ravendb:
    ports:
      - "18080:8080"  # Different port to avoid conflicts
```

**Start with local overrides:**
```bash
docker compose -f docker-compose.yml \
               -f docker-compose.override.yml \
               -f docker-compose.local.yml \
               up -d
```

### Production Override Example

For reference, a production override might look like:

```yaml
# docker-compose.prod.yml
version: '3.8'

services:
  ravendb:
    environment:
      - RAVEN_Security_UnsecuredAccessAllowed=None
    volumes:
      - /secure/path/license.json:/opt/RavenDB/Server/license.json:ro

  minio:
    environment:
      - MINIO_ROOT_USER=${PROD_MINIO_USER}
      - MINIO_ROOT_PASSWORD=${PROD_MINIO_PASS}
```

## Verification

```bash
# Check effective configuration
docker compose config

# Start services with overrides
docker compose up -d

# Verify resource limits are applied
docker stats --no-stream novatune-kafka novatune-rabbitmq novatune-ncache
```

## Troubleshooting

### Override Not Applied

Ensure file is named exactly `docker-compose.override.yml` (case-sensitive).

### Resource Limits Ignored

Resource limits require Docker Compose V2 with deploy support:

```bash
# Check Docker Compose version
docker compose version

# Should be v2.x.x or higher
```

### Variable Substitution Not Working

Verify `.env` file exists in the same directory:

```bash
ls -la .env

# Check parsed variables
docker compose config | grep MINIO
```

## Navigation

[Previous: Step 1.3.1 - Docker Compose File](step-1.3.1-docker-compose.md) | [Overview](overview.md) | [Next: Step 1.3.3 - Environment Variables](step-1.3.3-environment-variables.md)
