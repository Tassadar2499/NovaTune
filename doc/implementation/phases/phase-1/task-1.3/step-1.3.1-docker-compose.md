# Step 1.3.1: Create Main Docker Compose File

> **Parent Task:** [Task 1.3: Docker Compose Infrastructure](overview.md)
> **Status:** Completed
> **Output:** `docker-compose.yml`

## Objective

Create the main Docker Compose configuration file that defines all infrastructure services required by NovaTune. This file serves as the single source of truth for local development infrastructure.

## Background

Docker Compose allows us to define and run multi-container applications. For NovaTune, we need five core infrastructure services:

1. **RavenDB** - Document database for storing users, tracks, and metadata
2. **MinIO** - S3-compatible object storage for audio files
3. **Kafka** - Event streaming for analytics and async processing
4. **RabbitMQ** - Message broker for task queues
5. **NCache** - Distributed caching for sessions and presigned URLs

## Implementation Steps

### 1. Create the Docker Compose File

Create `docker-compose.yml` in the repository root:

```yaml
version: '3.8'

services:
  ravendb:
    image: ravendb/ravendb:6.0-ubuntu-latest
    container_name: novatune-ravendb
    ports:
      - "8080:8080"    # RavenDB Studio (Web UI)
      - "38888:38888"  # Database TCP connection
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
    networks:
      - novatune-network

  minio:
    image: minio/minio:latest
    container_name: novatune-minio
    ports:
      - "9000:9000"   # S3 API endpoint
      - "9001:9001"   # MinIO Console (Web UI)
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
    networks:
      - novatune-network

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
    networks:
      - novatune-network

  rabbitmq:
    image: rabbitmq:3-management
    container_name: novatune-rabbitmq
    ports:
      - "5672:5672"   # AMQP protocol
      - "15672:15672" # Management UI
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
    networks:
      - novatune-network

  ncache:
    image: alachisoft/ncache:latest
    container_name: novatune-ncache
    ports:
      - "8250:8250"   # Management Web UI
      - "9800:9800"   # Client connection port
    volumes:
      - ncache-data:/opt/ncache
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8250/"]
      interval: 30s
      timeout: 10s
      retries: 5
    networks:
      - novatune-network

networks:
  novatune-network:
    driver: bridge

volumes:
  ravendb-data:
  minio-data:
  kafka-data:
  rabbitmq-data:
  ncache-data:
```

## Service Details

### RavenDB Configuration

| Setting | Value | Description |
|---------|-------|-------------|
| Image | `ravendb/ravendb:6.0-ubuntu-latest` | RavenDB 6.0 on Ubuntu |
| Port 8080 | Studio UI | Web interface for database management |
| Port 38888 | TCP | Client connection port |
| `RAVEN_Setup_Mode=None` | Skips wizard | No interactive setup required |
| `RAVEN_License_Eula_Accepted=true` | Accept EULA | Required for container startup |
| `RAVEN_Security_UnsecuredAccessAllowed` | Development mode | Allows unauthenticated local access |

**Access RavenDB Studio:** http://localhost:8080

### MinIO Configuration

| Setting | Value | Description |
|---------|-------|-------------|
| Image | `minio/minio:latest` | Latest MinIO release |
| Port 9000 | S3 API | Compatible with AWS S3 SDK |
| Port 9001 | Console | Web UI for bucket management |
| Root credentials | `minioadmin/minioadmin` | Default dev credentials |

**Access MinIO Console:** http://localhost:9001

### Kafka Configuration (KRaft Mode)

| Setting | Value | Description |
|---------|-------|-------------|
| Image | `confluentinc/cp-kafka:7.5.0` | Confluent Platform Kafka |
| Port 9092 | Broker | Client connection port |
| Mode | KRaft | No ZooKeeper required |
| `KAFKA_NODE_ID` | 1 | Single node cluster |
| `KAFKA_PROCESS_ROLES` | `broker,controller` | Combined mode |
| `CLUSTER_ID` | Fixed UUID | Consistent cluster identity |

**Note:** Using KRaft mode eliminates ZooKeeper dependency, simplifying the stack.

### RabbitMQ Configuration

| Setting | Value | Description |
|---------|-------|-------------|
| Image | `rabbitmq:3-management` | Includes management plugin |
| Port 5672 | AMQP | Message protocol port |
| Port 15672 | Management | Web UI port |
| Credentials | `guest/guest` | Default dev credentials |

**Access RabbitMQ Management:** http://localhost:15672

### NCache Configuration

| Setting | Value | Description |
|---------|-------|-------------|
| Image | `alachisoft/ncache:latest` | NCache Community Edition |
| Port 8250 | Management | Web management console |
| Port 9800 | Client | Cache client connection |

**Access NCache Management:** http://localhost:8250

## Health Checks Explained

Each service includes a health check to ensure proper startup:

```yaml
healthcheck:
  test: ["CMD", "..."]   # Command to verify health
  interval: 30s          # Time between checks
  timeout: 10s           # Max time for check to complete
  retries: 5             # Failures before unhealthy
```

Health check commands:
- **RavenDB:** `curl -f http://localhost:8080/databases`
- **MinIO:** `curl -f http://localhost:9000/minio/health/live`
- **Kafka:** `kafka-broker-api-versions --bootstrap-server localhost:9092`
- **RabbitMQ:** `rabbitmqctl status`
- **NCache:** `curl -f http://localhost:8250/`

## Volumes

Named volumes persist data across container restarts:

| Volume | Purpose | Location in Container |
|--------|---------|----------------------|
| `ravendb-data` | Database files | `/opt/RavenDB/Server/RavenData` |
| `minio-data` | Object storage | `/data` |
| `kafka-data` | Message logs | `/var/lib/kafka/data` |
| `rabbitmq-data` | Queue data | `/var/lib/rabbitmq` |
| `ncache-data` | Cache data | `/opt/ncache` |

## Usage

```bash
# Start all services
docker compose up -d

# Start specific service
docker compose up -d ravendb

# View logs
docker compose logs -f kafka

# Stop services (preserve data)
docker compose down

# Stop and remove all data
docker compose down -v
```

## Troubleshooting

### Port Conflicts

If a port is already in use:

```bash
# Find what's using port 8080
lsof -i :8080

# Or on Linux
netstat -tulpn | grep 8080
```

### Container Won't Start

Check logs for specific service:

```bash
docker compose logs ravendb
```

### Out of Memory

Kafka and RavenDB require significant memory. Ensure Docker has at least 8 GB allocated in Docker Desktop settings.

## Verification

```bash
# Verify all containers are running
docker compose ps

# Expected output:
# NAME                 STATUS    PORTS
# novatune-kafka       Up        0.0.0.0:9092->9092/tcp
# novatune-minio       Up        0.0.0.0:9000-9001->9000-9001/tcp
# novatune-ncache      Up        0.0.0.0:8250->8250/tcp, 0.0.0.0:9800->9800/tcp
# novatune-rabbitmq    Up        0.0.0.0:5672->5672/tcp, 0.0.0.0:15672->15672/tcp
# novatune-ravendb     Up        0.0.0.0:8080->8080/tcp, 0.0.0.0:38888->38888/tcp
```

## Navigation

[Overview](overview.md) | [Next: Step 1.3.2 - Docker Compose Override](step-1.3.2-docker-compose-override.md)
