# Step 1.3.6: Create Startup Wait Script

> **Parent Task:** [Task 1.3: Docker Compose Infrastructure](overview.md)
> **Status:** Pending
> **Output:** `scripts/wait-for-services.sh`

## Objective

Create a script that waits for all infrastructure services to become ready before proceeding. This is essential for CI/CD pipelines and automated testing where services need to be fully operational before running tests.

## Background

Container health checks run inside containers, but external applications need to wait until services are actually accepting connections. The wait script:

1. Polls each service endpoint until it responds
2. Provides feedback during the waiting period
3. Fails gracefully with clear error messages
4. Supports configurable timeouts and retry logic

## Implementation Steps

### 1. Create the Wait Script

Create `scripts/wait-for-services.sh`:

```bash
#!/bin/bash
# =============================================================================
# NovaTune Service Startup Wait Script
# =============================================================================
# Waits for all infrastructure services to become ready.
# Useful for CI/CD pipelines and automated testing.
#
# Usage:
#   ./scripts/wait-for-services.sh [options]
#
# Options:
#   -t, --timeout SECONDS    Total timeout (default: 120)
#   -i, --interval SECONDS   Retry interval (default: 2)
#   -s, --services LIST      Comma-separated list of services to wait for
#   -q, --quiet              Suppress progress output
#   -h, --help               Show help
#
# Exit codes:
#   0 - All services ready
#   1 - Timeout reached
#   2 - Configuration error
# =============================================================================

set -e

# Configuration defaults
MAX_TIMEOUT=${WAIT_TIMEOUT:-120}
RETRY_INTERVAL=${WAIT_INTERVAL:-2}
QUIET=false
SERVICES_TO_CHECK="ravendb,minio,kafka,rabbitmq,ncache"

# Service endpoints (can be overridden via environment)
RAVENDB_CHECK_URL="${RAVENDB_URL:-http://localhost:8080}/databases"
MINIO_CHECK_URL="http://${MINIO_ENDPOINT:-localhost:9000}/minio/health/live"
KAFKA_BOOTSTRAP="${KAFKA_BOOTSTRAP_SERVERS:-localhost:9092}"
RABBITMQ_CHECK_URL="http://${RABBITMQ_HOST:-localhost}:15672/api/health/checks/alarms"
RABBITMQ_USER="${RABBITMQ_USERNAME:-guest}"
RABBITMQ_PASS="${RABBITMQ_PASSWORD:-guest}"
NCACHE_CHECK_URL="http://localhost:8250/"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# -----------------------------------------------------------------------------
# Helper Functions
# -----------------------------------------------------------------------------

log() {
    if [ "$QUIET" = "false" ]; then
        echo -e "$1"
    fi
}

log_progress() {
    if [ "$QUIET" = "false" ]; then
        echo -ne "\r$1"
    fi
}

show_help() {
    sed -n '2,20p' "$0" | sed 's/^# //' | sed 's/^#//'
    exit 0
}

# -----------------------------------------------------------------------------
# Service Wait Functions
# -----------------------------------------------------------------------------

wait_for_http() {
    local name=$1
    local url=$2
    local auth=$3
    local elapsed=0

    log "${BLUE}Waiting for $name...${NC}"

    while [ $elapsed -lt $MAX_TIMEOUT ]; do
        local curl_opts="-sf --connect-timeout 5"
        [ -n "$auth" ] && curl_opts="$curl_opts -u $auth"

        if curl $curl_opts "$url" > /dev/null 2>&1; then
            log "${GREEN}✓ $name is ready (${elapsed}s)${NC}"
            return 0
        fi

        log_progress "${YELLOW}Waiting for $name... ${elapsed}s/${MAX_TIMEOUT}s${NC}"
        sleep $RETRY_INTERVAL
        elapsed=$((elapsed + RETRY_INTERVAL))
    done

    log "\n${RED}✗ $name failed to start within ${MAX_TIMEOUT}s${NC}"
    return 1
}

wait_for_tcp() {
    local name=$1
    local host=$2
    local port=$3
    local elapsed=0

    log "${BLUE}Waiting for $name...${NC}"

    while [ $elapsed -lt $MAX_TIMEOUT ]; do
        if nc -z "$host" "$port" 2>/dev/null; then
            log "${GREEN}✓ $name is ready (${elapsed}s)${NC}"
            return 0
        fi

        log_progress "${YELLOW}Waiting for $name... ${elapsed}s/${MAX_TIMEOUT}s${NC}"
        sleep $RETRY_INTERVAL
        elapsed=$((elapsed + RETRY_INTERVAL))
    done

    log "\n${RED}✗ $name failed to start within ${MAX_TIMEOUT}s${NC}"
    return 1
}

wait_for_kafka() {
    local elapsed=0
    local host=$(echo $KAFKA_BOOTSTRAP | cut -d: -f1)
    local port=$(echo $KAFKA_BOOTSTRAP | cut -d: -f2)

    log "${BLUE}Waiting for Kafka...${NC}"

    # First wait for TCP port
    while [ $elapsed -lt $MAX_TIMEOUT ]; do
        if nc -z "$host" "$port" 2>/dev/null; then
            # Then verify broker is responding
            if docker exec novatune-kafka kafka-broker-api-versions \
                --bootstrap-server localhost:9092 > /dev/null 2>&1; then
                log "${GREEN}✓ Kafka is ready (${elapsed}s)${NC}"
                return 0
            fi
        fi

        log_progress "${YELLOW}Waiting for Kafka... ${elapsed}s/${MAX_TIMEOUT}s${NC}"
        sleep $RETRY_INTERVAL
        elapsed=$((elapsed + RETRY_INTERVAL))
    done

    log "\n${RED}✗ Kafka failed to start within ${MAX_TIMEOUT}s${NC}"
    return 1
}

# -----------------------------------------------------------------------------
# Service Dispatcher
# -----------------------------------------------------------------------------

wait_for_service() {
    local service=$1

    case $service in
        ravendb)
            wait_for_http "RavenDB" "$RAVENDB_CHECK_URL"
            ;;
        minio)
            wait_for_http "MinIO" "$MINIO_CHECK_URL"
            ;;
        kafka)
            wait_for_kafka
            ;;
        rabbitmq)
            wait_for_http "RabbitMQ" "$RABBITMQ_CHECK_URL" "$RABBITMQ_USER:$RABBITMQ_PASS"
            ;;
        ncache)
            wait_for_http "NCache" "$NCACHE_CHECK_URL"
            ;;
        *)
            log "${RED}Unknown service: $service${NC}"
            return 2
            ;;
    esac
}

# -----------------------------------------------------------------------------
# Main Execution
# -----------------------------------------------------------------------------

main() {
    local start_time=$(date +%s)
    local failed_services=()

    log ""
    log "============================================="
    log " Waiting for NovaTune Infrastructure"
    log "============================================="
    log " Timeout: ${MAX_TIMEOUT}s | Interval: ${RETRY_INTERVAL}s"
    log "============================================="
    log ""

    # Check prerequisites
    if ! command -v curl &> /dev/null; then
        log "${RED}Error: curl is required${NC}"
        exit 2
    fi

    # Check if Docker is running
    if ! docker info > /dev/null 2>&1; then
        log "${RED}Error: Docker is not running${NC}"
        exit 2
    fi

    # Check if containers exist
    local running=$(docker compose ps --status running -q 2>/dev/null | wc -l)
    if [ "$running" -eq 0 ]; then
        log "${YELLOW}Warning: No containers are running${NC}"
        log "Start containers with: docker compose up -d"
        exit 1
    fi

    # Wait for each service
    IFS=',' read -ra services <<< "$SERVICES_TO_CHECK"
    for service in "${services[@]}"; do
        service=$(echo "$service" | tr -d ' ')
        if ! wait_for_service "$service"; then
            failed_services+=("$service")
        fi
    done

    # Calculate elapsed time
    local end_time=$(date +%s)
    local total_time=$((end_time - start_time))

    log ""
    log "============================================="

    if [ ${#failed_services[@]} -eq 0 ]; then
        log "${GREEN}All services are ready! (${total_time}s total)${NC}"
        log "============================================="
        exit 0
    else
        log "${RED}Failed services: ${failed_services[*]}${NC}"
        log "============================================="
        log ""
        log "Troubleshooting:"
        log "  1. Check container status: docker compose ps"
        log "  2. View logs: docker compose logs ${failed_services[0]}"
        log "  3. Increase timeout: $0 -t 180"
        exit 1
    fi
}

# -----------------------------------------------------------------------------
# Argument Parsing
# -----------------------------------------------------------------------------

while [[ $# -gt 0 ]]; do
    case $1 in
        -t|--timeout)
            MAX_TIMEOUT="$2"
            shift 2
            ;;
        -i|--interval)
            RETRY_INTERVAL="$2"
            shift 2
            ;;
        -s|--services)
            SERVICES_TO_CHECK="$2"
            shift 2
            ;;
        -q|--quiet)
            QUIET=true
            shift
            ;;
        -h|--help)
            show_help
            ;;
        *)
            echo "Unknown option: $1"
            exit 2
            ;;
    esac
done

main
```

### 2. Make Script Executable

```bash
chmod +x scripts/wait-for-services.sh
```

## Usage

### Basic Usage

```bash
# Wait for all services with defaults (120s timeout)
./scripts/wait-for-services.sh

# Custom timeout
./scripts/wait-for-services.sh --timeout 180

# Wait for specific services only
./scripts/wait-for-services.sh --services "ravendb,minio"

# Quiet mode (for CI)
./scripts/wait-for-services.sh --quiet
```

### Sample Output

```
=============================================
 Waiting for NovaTune Infrastructure
=============================================
 Timeout: 120s | Interval: 2s
=============================================

Waiting for RavenDB...
✓ RavenDB is ready (4s)
Waiting for MinIO...
✓ MinIO is ready (2s)
Waiting for Kafka...
✓ Kafka is ready (8s)
Waiting for RabbitMQ...
✓ RabbitMQ is ready (6s)
Waiting for NCache...
✓ NCache is ready (4s)

=============================================
All services are ready! (24s total)
=============================================
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `WAIT_TIMEOUT` | 120 | Maximum wait time in seconds |
| `WAIT_INTERVAL` | 2 | Seconds between retry attempts |
| `RAVENDB_URL` | `http://localhost:8080` | RavenDB base URL |
| `MINIO_ENDPOINT` | `localhost:9000` | MinIO endpoint |
| `KAFKA_BOOTSTRAP_SERVERS` | `localhost:9092` | Kafka bootstrap servers |
| `RABBITMQ_HOST` | `localhost` | RabbitMQ host |
| `RABBITMQ_USERNAME` | `guest` | RabbitMQ username |
| `RABBITMQ_PASSWORD` | `guest` | RabbitMQ password |

## Integration Patterns

### CI/CD Pipeline

```yaml
# GitHub Actions
jobs:
  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Start infrastructure
        run: docker compose up -d

      - name: Wait for services
        run: ./scripts/wait-for-services.sh --timeout 180

      - name: Run integration tests
        run: dotnet test --filter Category=Integration
```

### Makefile

```makefile
.PHONY: infra-up
infra-up:
	docker compose up -d
	./scripts/wait-for-services.sh

.PHONY: test-integration
test-integration: infra-up
	dotnet test --filter Category=Integration

.PHONY: dev
dev: infra-up
	dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost
```

### Docker Compose Healthcheck Override

For more reliable waits, combine with Docker healthchecks:

```yaml
# docker-compose.yml
services:
  api:
    build: .
    depends_on:
      ravendb:
        condition: service_healthy
      minio:
        condition: service_healthy
      kafka:
        condition: service_healthy
```

### Shell Script Wrapper

```bash
#!/bin/bash
# scripts/dev-start.sh

echo "Starting NovaTune development environment..."

# Start infrastructure
docker compose up -d

# Wait for readiness
./scripts/wait-for-services.sh || {
    echo "Failed to start infrastructure"
    docker compose logs
    exit 1
}

# Start the application
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost
```

## Comparison: Wait vs Health Check

| Script | Purpose | Use Case |
|--------|---------|----------|
| `wait-for-services.sh` | Block until ready | CI/CD, automated startup |
| `healthcheck.sh` | Point-in-time status | Debugging, monitoring |

Typical workflow:
1. `docker compose up -d` - Start containers
2. `./scripts/wait-for-services.sh` - Wait for readiness
3. Run tests or application
4. `./scripts/healthcheck.sh` - Verify during development

## Troubleshooting

### Timeout on Slow Systems

```bash
# Increase timeout to 5 minutes
./scripts/wait-for-services.sh --timeout 300 --interval 5
```

### Service Never Becomes Ready

```bash
# Check container logs
docker compose logs ravendb

# Check container status
docker compose ps

# Try restarting the service
docker compose restart ravendb
```

### Network Issues

```bash
# Verify Docker network
docker network ls
docker network inspect novatune_novatune-network

# Check port bindings
docker compose port ravendb 8080
```

## Navigation

[Previous: Step 1.3.5 - Health Check Scripts](step-1.3.5-health-check-scripts.md) | [Overview](overview.md) | [Next: Step 1.3.7 - Dev Containers](step-1.3.7-dev-containers.md)
