# Step 1.3.5: Create Health Check Scripts

> **Parent Task:** [Task 1.3: Docker Compose Infrastructure](overview.md)
> **Status:** Pending
> **Output:** `scripts/healthcheck.sh`

## Objective

Create a comprehensive health check script that verifies all infrastructure services are running and responding correctly. This script provides quick feedback on infrastructure status and helps diagnose connectivity issues.

## Background

Health checks serve multiple purposes:

1. **Quick verification** - Confirm all services started successfully
2. **CI/CD integration** - Gate deployments on infrastructure health
3. **Debugging** - Identify which service is failing
4. **Monitoring** - Periodic checks in development

## Implementation Steps

### 1. Create the Health Check Script

Create `scripts/healthcheck.sh`:

```bash
#!/bin/bash
# =============================================================================
# NovaTune Infrastructure Health Check
# =============================================================================
# Verifies all infrastructure services are running and responding.
# Exit codes:
#   0 - All services healthy
#   1 - One or more services unhealthy
# =============================================================================

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
TIMEOUT=5
VERBOSE=${VERBOSE:-false}

# Track failures
FAILED_SERVICES=()

# -----------------------------------------------------------------------------
# Helper Functions
# -----------------------------------------------------------------------------

log_info() {
    echo -e "${NC}[INFO] $1${NC}"
}

log_success() {
    echo -e "${GREEN}[OK]${NC} $1"
}

log_error() {
    echo -e "${RED}[FAIL]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

check_command() {
    if ! command -v "$1" &> /dev/null; then
        log_error "Required command not found: $1"
        exit 1
    fi
}

# -----------------------------------------------------------------------------
# Service Check Functions
# -----------------------------------------------------------------------------

check_ravendb() {
    local url="${RAVENDB_URL:-http://localhost:8080}"
    log_info "Checking RavenDB at $url..."

    if curl -sf --connect-timeout $TIMEOUT "$url/databases" > /dev/null 2>&1; then
        log_success "RavenDB is healthy"

        # Additional checks if verbose
        if [ "$VERBOSE" = "true" ]; then
            local stats=$(curl -sf "$url/admin/stats" 2>/dev/null || echo "{}")
            echo "  - Server version: $(echo $stats | grep -o '"ServerVersion":"[^"]*"' | cut -d'"' -f4)"
        fi
        return 0
    else
        log_error "RavenDB is not responding"
        FAILED_SERVICES+=("RavenDB")
        return 1
    fi
}

check_minio() {
    local endpoint="${MINIO_ENDPOINT:-localhost:9000}"
    local url="http://$endpoint/minio/health/live"
    log_info "Checking MinIO at $url..."

    if curl -sf --connect-timeout $TIMEOUT "$url" > /dev/null 2>&1; then
        log_success "MinIO is healthy"

        # Check console as well
        if curl -sf --connect-timeout $TIMEOUT "http://localhost:9001" > /dev/null 2>&1; then
            [ "$VERBOSE" = "true" ] && echo "  - Console is accessible"
        fi
        return 0
    else
        log_error "MinIO is not responding"
        FAILED_SERVICES+=("MinIO")
        return 1
    fi
}

check_kafka() {
    local bootstrap="${KAFKA_BOOTSTRAP_SERVERS:-localhost:9092}"
    log_info "Checking Kafka at $bootstrap..."

    # Method 1: Use docker exec if container exists
    if docker ps --format '{{.Names}}' | grep -q "novatune-kafka"; then
        if docker exec novatune-kafka kafka-broker-api-versions \
            --bootstrap-server localhost:9092 > /dev/null 2>&1; then
            log_success "Kafka is healthy"

            if [ "$VERBOSE" = "true" ]; then
                local topics=$(docker exec novatune-kafka kafka-topics \
                    --bootstrap-server localhost:9092 --list 2>/dev/null | wc -l)
                echo "  - Topics count: $topics"
            fi
            return 0
        fi
    fi

    # Method 2: TCP port check as fallback
    if nc -z localhost 9092 2>/dev/null; then
        log_success "Kafka port is open (basic check)"
        return 0
    fi

    log_error "Kafka is not responding"
    FAILED_SERVICES+=("Kafka")
    return 1
}

check_rabbitmq() {
    local host="${RABBITMQ_HOST:-localhost}"
    local port="${RABBITMQ_PORT:-15672}"
    local user="${RABBITMQ_USERNAME:-guest}"
    local pass="${RABBITMQ_PASSWORD:-guest}"
    local url="http://$host:$port/api/health/checks/alarms"

    log_info "Checking RabbitMQ at $host:$port..."

    if curl -sf --connect-timeout $TIMEOUT -u "$user:$pass" "$url" > /dev/null 2>&1; then
        log_success "RabbitMQ is healthy"

        if [ "$VERBOSE" = "true" ]; then
            local overview=$(curl -sf -u "$user:$pass" "http://$host:$port/api/overview" 2>/dev/null)
            echo "  - Version: $(echo $overview | grep -o '"rabbitmq_version":"[^"]*"' | cut -d'"' -f4)"
            echo "  - Queues: $(echo $overview | grep -o '"queue_totals":{[^}]*}' | grep -o '"messages":[0-9]*' | cut -d':' -f2)"
        fi
        return 0
    else
        log_error "RabbitMQ is not responding"
        FAILED_SERVICES+=("RabbitMQ")
        return 1
    fi
}

check_ncache() {
    local server="${NCACHE_SERVER:-localhost:9800}"
    local mgmt_url="http://localhost:8250"

    log_info "Checking NCache at $mgmt_url..."

    if curl -sf --connect-timeout $TIMEOUT "$mgmt_url/" > /dev/null 2>&1; then
        log_success "NCache is healthy"
        return 0
    else
        # Fallback: check if port is open
        if nc -z localhost 9800 2>/dev/null; then
            log_warning "NCache management UI not responding, but client port is open"
            return 0
        fi

        log_error "NCache is not responding"
        FAILED_SERVICES+=("NCache")
        return 1
    fi
}

# -----------------------------------------------------------------------------
# Docker Health Status
# -----------------------------------------------------------------------------

check_docker_health() {
    log_info "Checking Docker container health status..."
    echo ""

    # Get container health status
    docker compose ps --format "table {{.Name}}\t{{.Status}}\t{{.Health}}" 2>/dev/null || \
        docker compose ps 2>/dev/null || \
        log_warning "Could not get container status"

    echo ""
}

# -----------------------------------------------------------------------------
# Main Execution
# -----------------------------------------------------------------------------

main() {
    echo "============================================="
    echo " NovaTune Infrastructure Health Check"
    echo "============================================="
    echo ""

    # Check prerequisites
    check_command "curl"
    check_command "docker"

    # Show Docker status
    check_docker_health

    # Run individual checks
    echo "Running service health checks..."
    echo ""

    check_ravendb || true
    check_minio || true
    check_kafka || true
    check_rabbitmq || true
    check_ncache || true

    echo ""
    echo "============================================="

    # Summary
    if [ ${#FAILED_SERVICES[@]} -eq 0 ]; then
        echo -e "${GREEN}All services are healthy!${NC}"
        echo "============================================="
        exit 0
    else
        echo -e "${RED}Failed services: ${FAILED_SERVICES[*]}${NC}"
        echo "============================================="
        echo ""
        echo "Troubleshooting tips:"
        echo "  1. Check if containers are running: docker compose ps"
        echo "  2. View container logs: docker compose logs <service>"
        echo "  3. Restart failed service: docker compose restart <service>"
        echo ""
        exit 1
    fi
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  -v, --verbose    Show additional service details"
            echo "  -h, --help       Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

main
```

### 2. Make Script Executable

```bash
chmod +x scripts/healthcheck.sh
```

### 3. Create Individual Service Check Scripts (Optional)

For granular checks, create service-specific scripts:

**`scripts/check-ravendb.sh`:**
```bash
#!/bin/bash
curl -sf "${RAVENDB_URL:-http://localhost:8080}/databases" > /dev/null
exit $?
```

**`scripts/check-minio.sh`:**
```bash
#!/bin/bash
curl -sf "http://${MINIO_ENDPOINT:-localhost:9000}/minio/health/live" > /dev/null
exit $?
```

## Usage

### Basic Usage

```bash
# Run health check
./scripts/healthcheck.sh

# Run with verbose output
./scripts/healthcheck.sh --verbose
# or
VERBOSE=true ./scripts/healthcheck.sh
```

### Sample Output

**Healthy state:**
```
=============================================
 NovaTune Infrastructure Health Check
=============================================

Checking Docker container health status...

NAME                 STATUS          HEALTH
novatune-kafka       Up 2 minutes    healthy
novatune-minio       Up 2 minutes    healthy
novatune-ncache      Up 2 minutes    healthy
novatune-rabbitmq    Up 2 minutes    healthy
novatune-ravendb     Up 2 minutes    healthy

Running service health checks...

[INFO] Checking RavenDB at http://localhost:8080...
[OK] RavenDB is healthy
[INFO] Checking MinIO at http://localhost:9000/minio/health/live...
[OK] MinIO is healthy
[INFO] Checking Kafka at localhost:9092...
[OK] Kafka is healthy
[INFO] Checking RabbitMQ at localhost:15672...
[OK] RabbitMQ is healthy
[INFO] Checking NCache at http://localhost:8250...
[OK] NCache is healthy

=============================================
All services are healthy!
=============================================
```

**Unhealthy state:**
```
=============================================
 NovaTune Infrastructure Health Check
=============================================

...

[INFO] Checking RavenDB at http://localhost:8080...
[OK] RavenDB is healthy
[INFO] Checking MinIO at http://localhost:9000/minio/health/live...
[FAIL] MinIO is not responding
[INFO] Checking Kafka at localhost:9092...
[OK] Kafka is healthy
...

=============================================
Failed services: MinIO
=============================================

Troubleshooting tips:
  1. Check if containers are running: docker compose ps
  2. View container logs: docker compose logs <service>
  3. Restart failed service: docker compose restart <service>
```

## Integration Points

### CI/CD Pipeline

```yaml
# GitHub Actions example
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Start infrastructure
        run: docker compose up -d

      - name: Wait for services
        run: ./scripts/wait-for-services.sh

      - name: Health check
        run: ./scripts/healthcheck.sh

      - name: Run tests
        run: dotnet test
```

### Pre-commit Hook

```bash
# .git/hooks/pre-push
#!/bin/bash
./scripts/healthcheck.sh || {
    echo "Infrastructure not healthy. Please start services."
    exit 1
}
```

### Makefile Integration

```makefile
.PHONY: health
health:
	@./scripts/healthcheck.sh

.PHONY: start
start:
	docker compose up -d
	./scripts/wait-for-services.sh
	./scripts/healthcheck.sh
```

## Extending Health Checks

### Add Custom Service

To add a new service health check:

```bash
check_custom_service() {
    local url="http://localhost:PORT/health"
    log_info "Checking CustomService at $url..."

    if curl -sf --connect-timeout $TIMEOUT "$url" > /dev/null 2>&1; then
        log_success "CustomService is healthy"
        return 0
    else
        log_error "CustomService is not responding"
        FAILED_SERVICES+=("CustomService")
        return 1
    fi
}

# Add to main()
check_custom_service || true
```

### Add Deep Health Check

For more thorough verification:

```bash
check_ravendb_deep() {
    # Basic connectivity
    check_ravendb || return 1

    # Check database exists
    local db_url="${RAVENDB_URL:-http://localhost:8080}/databases/NovaTune"
    if ! curl -sf "$db_url/stats" > /dev/null 2>&1; then
        log_warning "Database 'NovaTune' not found (may need initialization)"
    fi

    # Check indexes
    local indexes=$(curl -sf "$db_url/indexes" 2>/dev/null | grep -c '"Name"' || echo "0")
    log_info "  - Indexes: $indexes"
}
```

## Navigation

[Previous: Step 1.3.4 - Resource Requirements](step-1.3.4-resource-requirements.md) | [Overview](overview.md) | [Next: Step 1.3.6 - Wait Scripts](step-1.3.6-wait-scripts.md)
