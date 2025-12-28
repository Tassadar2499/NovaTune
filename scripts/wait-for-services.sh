#!/bin/bash
# =============================================================================
# NovaTune Wait for Services
# =============================================================================
# Waits until all infrastructure services are ready.
# Useful for CI/CD pipelines and startup scripts.
# =============================================================================

set -e

# Change to repository root
cd "$(dirname "$0")/.."

MAX_RETRIES=${MAX_RETRIES:-30}
RETRY_INTERVAL=${RETRY_INTERVAL:-2}

echo "=========================================="
echo "Waiting for NovaTune Infrastructure"
echo "=========================================="
echo "Max retries: $MAX_RETRIES"
echo "Retry interval: ${RETRY_INTERVAL}s"
echo ""

wait_for_http() {
    local name=$1
    local url=$2
    local auth=$3
    local retries=0

    printf "Waiting for %-12s " "$name"

    while [ $retries -lt $MAX_RETRIES ]; do
        if [ -n "$auth" ]; then
            if curl -sf "$url" -u "$auth" > /dev/null 2>&1; then
                echo "✓ ready"
                return 0
            fi
        else
            if curl -sf "$url" > /dev/null 2>&1; then
                echo "✓ ready"
                return 0
            fi
        fi

        retries=$((retries + 1))
        printf "."
        sleep $RETRY_INTERVAL
    done

    echo " ✗ timeout"
    return 1
}

wait_for_kafka() {
    local retries=0

    printf "Waiting for %-12s " "Kafka"

    while [ $retries -lt $MAX_RETRIES ]; do
        if docker exec novatune-kafka kafka-broker-api-versions --bootstrap-server localhost:9092 > /dev/null 2>&1; then
            echo "✓ ready"
            return 0
        fi

        retries=$((retries + 1))
        printf "."
        sleep $RETRY_INTERVAL
    done

    echo " ✗ timeout"
    return 1
}

FAILED=0

# Wait for each service
wait_for_http "RavenDB" "http://localhost:8080/databases" || FAILED=1
wait_for_http "MinIO" "http://localhost:9000/minio/health/live" || FAILED=1
wait_for_http "RabbitMQ" "http://localhost:15672/api/health/checks/alarms" "guest:guest" || FAILED=1
wait_for_kafka || FAILED=1
wait_for_http "NCache" "http://localhost:8250/" || FAILED=1

echo ""
echo "=========================================="

if [ $FAILED -eq 0 ]; then
    echo "All services are ready!"
    exit 0
else
    echo ""
    echo "ERROR: Some services failed to start."
    echo ""
    echo "Troubleshooting:"
    echo "  1. Check if Docker is running"
    echo "  2. Check container status: docker compose ps"
    echo "  3. Check logs: docker compose logs"
    echo "  4. Ensure ports are not in use by other applications"
    exit 1
fi
