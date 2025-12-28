# Task 1.3: Docker Compose Infrastructure

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P1 (Must-have)
> **Status:** Completed

## Description

Set up Docker Compose for local infrastructure dependencies.

---

## Subtasks

### 1.3.1 Create Main Docker Compose File

- [x] Create `docker-compose.yml` with all services:

```yaml
version: '3.8'

services:
  ravendb:
    image: ravendb/ravendb:6.0-ubuntu-latest
    container_name: novatune-ravendb
    ports:
      - "8080:8080"    # Studio
      - "38888:38888"  # Database
    environment:
      - RAVEN_Setup_Mode=None
      - RAVEN_License_Eula_Accepted=true
      - RAVEN_Security_UnsecuredAccessAllowed=PrivateNetwork
    volumes:
      - ravendb-data:/opt/RavenDB/Server/RavenData
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/databases"]
      interval: 30s
      timeout: 10s
      retries: 5

  minio:
    image: minio/minio:latest
    container_name: novatune-minio
    ports:
      - "9000:9000"   # API
      - "9001:9001"   # Console
    environment:
      - MINIO_ROOT_USER=minioadmin
      - MINIO_ROOT_PASSWORD=minioadmin
    command: server /data --console-address ":9001"
    volumes:
      - minio-data:/data
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 30s
      timeout: 10s
      retries: 5

  kafka:
    image: confluentinc/cp-kafka:7.5.0
    container_name: novatune-kafka
    ports:
      - "9092:9092"
    environment:
      - KAFKA_NODE_ID=1
      - KAFKA_LISTENER_SECURITY_PROTOCOL_MAP=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT
      - KAFKA_LISTENERS=PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093
      - KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://localhost:9092
      - KAFKA_CONTROLLER_QUORUM_VOTERS=1@localhost:9093
      - KAFKA_PROCESS_ROLES=broker,controller
      - KAFKA_CONTROLLER_LISTENER_NAMES=CONTROLLER
      - KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1
      - CLUSTER_ID=MkU3OEVBNTcwNTJENDM2Qk
    volumes:
      - kafka-data:/var/lib/kafka/data
    healthcheck:
      test: ["CMD", "kafka-broker-api-versions", "--bootstrap-server", "localhost:9092"]
      interval: 30s
      timeout: 10s
      retries: 5

  rabbitmq:
    image: rabbitmq:3-management
    container_name: novatune-rabbitmq
    ports:
      - "5672:5672"   # AMQP
      - "15672:15672" # Management
    environment:
      - RABBITMQ_DEFAULT_USER=guest
      - RABBITMQ_DEFAULT_PASS=guest
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmqctl", "status"]
      interval: 30s
      timeout: 10s
      retries: 5

  ncache:
    image: alachisoft/ncache:latest
    container_name: novatune-ncache
    ports:
      - "8250:8250"   # Management
      - "9800:9800"   # Client
    volumes:
      - ncache-data:/opt/ncache
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8250/"]
      interval: 30s
      timeout: 10s
      retries: 5

volumes:
  ravendb-data:
  minio-data:
  kafka-data:
  rabbitmq-data:
  ncache-data:
```

---

### 1.3.2 Create Docker Compose Override

- [x] Create `docker-compose.override.yml` for development-specific settings:

```yaml
version: '3.8'

services:
  ravendb:
    environment:
      - RAVEN_License_Path=/opt/RavenDB/Server/license.json
    # Uncomment to mount license file
    # volumes:
    #   - ./config/ravendb-license.json:/opt/RavenDB/Server/license.json:ro

  minio:
    environment:
      - MINIO_ROOT_USER=${MINIO_ACCESS_KEY:-minioadmin}
      - MINIO_ROOT_PASSWORD=${MINIO_SECRET_KEY:-minioadmin}

  # Add resource limits for development
  kafka:
    deploy:
      resources:
        limits:
          memory: 1G

  ncache:
    deploy:
      resources:
        limits:
          memory: 512M
```

---

### 1.3.3 Create Environment Variables Template

- [x] Create `.env.example` with all required environment variables:

```bash
# ===========================================
# NovaTune Environment Configuration
# ===========================================
# Copy this file to .env and update values

# -------------------------------------------
# RavenDB Configuration
# -------------------------------------------
RAVENDB_URL=http://localhost:8080
RAVENDB_DATABASE=NovaTune

# -------------------------------------------
# MinIO (S3-Compatible Storage)
# -------------------------------------------
MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin
MINIO_BUCKET=novatune-dev-audio
MINIO_USE_SSL=false

# -------------------------------------------
# Kafka Configuration
# -------------------------------------------
KAFKA_BOOTSTRAP_SERVERS=localhost:9092
KAFKA_GROUP_ID=novatune-api

# -------------------------------------------
# RabbitMQ Configuration
# -------------------------------------------
RABBITMQ_HOST=localhost
RABBITMQ_PORT=5672
RABBITMQ_USERNAME=guest
RABBITMQ_PASSWORD=guest
RABBITMQ_VIRTUAL_HOST=/

# -------------------------------------------
# NCache Configuration
# -------------------------------------------
NCACHE_SERVER=localhost:9800
NCACHE_CACHE_NAME=novatune-cache

# -------------------------------------------
# JWT Configuration
# -------------------------------------------
JWT_ISSUER=https://novatune.local
JWT_AUDIENCE=novatune-api
JWT_SIGNING_KEY_PATH=./keys/signing.pem
JWT_ACCESS_TOKEN_EXPIRY_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRY_DAYS=7

# -------------------------------------------
# Application Settings
# -------------------------------------------
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=https://localhost:5001;http://localhost:5000
```

---

### 1.3.4 Document Resource Requirements

- [x] Document minimum resource requirements

**Minimum Requirements:**
| Resource | Minimum | Recommended |
|----------|---------|-------------|
| RAM | 8 GB | 16 GB |
| CPU | 4 cores | 8 cores |
| Disk | 20 GB | 50 GB |

**Per-Service Memory:**
| Service | Memory |
|---------|--------|
| RavenDB | 1-2 GB |
| MinIO | 512 MB |
| Kafka | 1 GB |
| RabbitMQ | 512 MB |
| NCache | 512 MB |

---

### 1.3.5 Create Health Check Scripts

- [x] Create `scripts/healthcheck.sh`:

```bash
#!/bin/bash
set -e

echo "Checking RavenDB..."
curl -sf http://localhost:8080/databases || { echo "RavenDB not ready"; exit 1; }

echo "Checking MinIO..."
curl -sf http://localhost:9000/minio/health/live || { echo "MinIO not ready"; exit 1; }

echo "Checking RabbitMQ..."
curl -sf http://localhost:15672/api/health/checks/alarms -u guest:guest || { echo "RabbitMQ not ready"; exit 1; }

echo "Checking Kafka..."
docker exec novatune-kafka kafka-broker-api-versions --bootstrap-server localhost:9092 > /dev/null 2>&1 || { echo "Kafka not ready"; exit 1; }

echo "Checking NCache..."
curl -sf http://localhost:8250/ || { echo "NCache not ready"; exit 1; }

echo "All services healthy!"
```

---

### 1.3.6 Create Startup Wait Script

- [x] Create `scripts/wait-for-services.sh`:

```bash
#!/bin/bash
set -e

MAX_RETRIES=30
RETRY_INTERVAL=2

wait_for_service() {
    local name=$1
    local url=$2
    local retries=0

    echo "Waiting for $name..."
    while [ $retries -lt $MAX_RETRIES ]; do
        if curl -sf "$url" > /dev/null 2>&1; then
            echo "$name is ready!"
            return 0
        fi
        retries=$((retries + 1))
        echo "Waiting for $name... ($retries/$MAX_RETRIES)"
        sleep $RETRY_INTERVAL
    done

    echo "ERROR: $name failed to start"
    return 1
}

# Wait for each service
wait_for_service "RavenDB" "http://localhost:8080/databases"
wait_for_service "MinIO" "http://localhost:9000/minio/health/live"
wait_for_service "RabbitMQ" "http://localhost:15672/api/health/checks/alarms"
wait_for_service "NCache" "http://localhost:8250/"

echo ""
echo "All services are ready!"
```

---

## Acceptance Criteria

- [x] `docker compose up` starts all services
- [x] All services pass health checks
- [x] `.env.example` documents all variables
- [x] README includes startup instructions

---

### 1.3.7 JetBrains Rider Dev Containers

> Goal: Develop inside a reproducible Docker dev container using JetBrains Rider while controlling local infra via Docker. This supports NF-8.1/NF-8.3 and aligns with the stack (Docker, Aspire) in `doc/requirements/stack.md`.

- [x] Prereqs
  - JetBrains Rider 2024.2+ with the Dev Containers plugin
  - Docker Desktop (or Docker Engine) installed and running
  - Git installed on host

- [x] Add `.devcontainer/devcontainer.json` (example)

```json
{
  "name": "NovaTune Dev",
  "image": "mcr.microsoft.com/devcontainers/dotnet:1-8.0-bookworm",
  "features": {
    "ghcr.io/devcontainers/features/docker-outside-of-docker:1": {}
  },
  "remoteUser": "vscode",
  "updateRemoteUserUID": true,
  "mounts": [
    "source=${localEnv:HOME}/.nuget/packages,target=/home/vscode/.nuget/packages,type=bind,consistency=cached"
  ],
  "postCreateCommand": "dotnet --info && dotnet restore",
  "forwardPorts": [8080, 9000, 9001, 9092, 15672, 8250, 9800],
  "portsAttributes": {
    "8080": { "label": "RavenDB" },
    "9000": { "label": "MinIO API" },
    "9001": { "label": "MinIO Console" },
    "9092": { "label": "Kafka" },
    "15672": { "label": "RabbitMQ Mgmt" },
    "8250": { "label": "NCache Mgmt" },
    "9800": { "label": "NCache Client" }
  },
  "customizations": {
    "jetbrains": {
      "ide": "Rider"
    }
  }
}
```

Notes
- The `docker-outside-of-docker` feature mounts the host Docker socket so you can run `docker compose` from inside the dev container (no separate DinD).
- The base image includes .NET SDK 8; Rider will use it for builds/tests. Adjust if targeting a newer SDK.

- [x] Open in Rider Dev Container
  1. In Rider: Tools → Dev Containers → Open Folder in Dev Container…
  2. Select the repo root (detects `.devcontainer/devcontainer.json`).
  3. Rider builds the container and attaches to it; open `src/NovaTuneApp/NovaTuneApp.sln`.

- [x] Start infra from inside the dev container

```bash
# From repo root inside the devcontainer shell
docker compose up -d            # or: docker compose up infra
./scripts/wait-for-services.sh  # optional: wait until healthy
```

- [x] Run the app in the dev container

```bash
dotnet restore
dotnet build
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost
# or just the API
dotnet run --project src/NovaTuneApp/NovaTuneApp.ApiService
```

Troubleshooting
- If `docker` permission denied: reopen the dev container (the feature adds your user to the docker group), or run a new shell.
- Port collisions: stop any host services bound to the same ports before `docker compose up`.
- Environment: copy `.env.example` to `.env` on the host; it will be visible inside the dev container.

---

## Verification Commands

```bash
# Start all services
docker compose up -d

# Check service status
docker compose ps

# Run health checks
./scripts/healthcheck.sh

# View logs
docker compose logs -f

# Stop all services
docker compose down
```

---

## File Checklist

- [x] `docker-compose.yml`
- [x] `docker-compose.override.yml`
- [x] `.env.example`
- [x] `scripts/healthcheck.sh`
- [x] `scripts/wait-for-services.sh`
- [x] `.devcontainer/devcontainer.json`

---

## Navigation

[Task 1.2: Domain Entities](task-1.2-domain-entities.md) | [Phase 1 Overview](overview.md) | [Task 1.4: Aspire AppHost](task-1.4-aspire-apphost.md)
