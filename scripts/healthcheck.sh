#!/bin/bash
# =============================================================================
# NovaTune Infrastructure Health Check
# =============================================================================
# Checks if all Docker Compose services are healthy.
# Exit code 0 = all healthy, non-zero = at least one service unhealthy.
# =============================================================================

set -e

# Change to repository root
cd "$(dirname "$0")/.."

echo "=========================================="
echo "NovaTune Infrastructure Health Check"
echo "=========================================="
echo ""

FAILED=0

check_service() {
    local name=$1
    local url=$2
    local auth=$3

    printf "Checking %-12s ... " "$name"

    if [ -n "$auth" ]; then
        if curl -sf "$url" -u "$auth" > /dev/null 2>&1; then
            echo "✓ OK"
            return 0
        fi
    else
        if curl -sf "$url" > /dev/null 2>&1; then
            echo "✓ OK"
            return 0
        fi
    fi

    echo "✗ FAILED"
    return 1
}

check_kafka() {
    printf "Checking %-12s ... " "Kafka"

    if docker exec novatune-kafka kafka-broker-api-versions --bootstrap-server localhost:9092 > /dev/null 2>&1; then
        echo "✓ OK"
        return 0
    fi

    echo "✗ FAILED"
    return 1
}

# Check each service
check_service "RavenDB" "http://localhost:8080/databases" || FAILED=1
check_service "MinIO" "http://localhost:9000/minio/health/live" || FAILED=1
check_service "RabbitMQ" "http://localhost:15672/api/health/checks/alarms" "guest:guest" || FAILED=1
check_kafka || FAILED=1
check_service "NCache" "http://localhost:8250/" || FAILED=1

echo ""
echo "=========================================="

if [ $FAILED -eq 0 ]; then
    echo "All services are healthy!"
    exit 0
else
    echo "Some services are not healthy."
    echo "Run 'docker compose logs' to investigate."
    exit 1
fi