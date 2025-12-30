# Phase 1: Infrastructure Changes (Docker Compose)

## 1.1 Replace Kafka + RabbitMQ with Redpanda
Update `docker-compose.yml`:

```yaml
# Remove: kafka, rabbitmq services
# Add:
redpanda:
  image: redpandadata/redpanda:v24.2.4
  container_name: novatune-redpanda
  command:
    - redpanda start
    - --smp 1
    - --memory 1G
    - --reserve-memory 0M
    - --overprovisioned
    - --node-id 0
    - --kafka-addr internal://0.0.0.0:9092,external://0.0.0.0:19092
    - --advertise-kafka-addr internal://redpanda:9092,external://localhost:19092
    - --pandaproxy-addr internal://0.0.0.0:8082,external://0.0.0.0:18082
    - --advertise-pandaproxy-addr internal://redpanda:8082,external://localhost:18082
    - --schema-registry-addr internal://0.0.0.0:8081,external://0.0.0.0:18081
    - --advertise-schema-registry-addr internal://redpanda:8081,external://localhost:18081
  ports:
    - "19092:19092"  # Kafka API (external)
    - "18082:18082"  # Pandaproxy (REST)
    - "18081:18081"  # Schema Registry (optional)
    - "9644:9644"    # Admin API
  volumes:
    - redpanda-data:/var/lib/redpanda/data
  healthcheck:
    test: ["CMD", "rpk", "cluster", "health", "--api-urls", "localhost:9644"]
    interval: 30s
    timeout: 10s
    retries: 5
  networks:
    - novatune-network
```

## 1.2 Replace NCache with Garnet
```yaml
# Remove: ncache service
# Add:
garnet:
  image: ghcr.io/microsoft/garnet:1.0.44
  container_name: novatune-garnet
  ports:
    - "6379:6379"
  volumes:
    - garnet-data:/data
  command: ["--checkpointdir", "/data/checkpoints", "--aof", "--aof-path", "/data/aof"]
  healthcheck:
    test: ["CMD", "redis-cli", "ping"]
    interval: 30s
    timeout: 10s
    retries: 5
  networks:
    - novatune-network
```

## 1.3 Update Volumes
```yaml
volumes:
  ravendb-data:
  minio-data:
  redpanda-data:    # was: kafka-data
  garnet-data:      # was: ncache-data
  # Remove: kafka-data, rabbitmq-data, ncache-data
```

## 1.4 Update docker-compose.override.yml
- Replace `kafka-ui` with Redpanda Console:
```yaml
redpanda-console:
  image: redpandadata/console:v2.7.2
  container_name: novatune-redpanda-console
  ports:
    - "8081:8080"
  environment:
    - KAFKA_BROKERS=redpanda:9092
  depends_on:
    - redpanda
  profiles:
    - debug
  networks:
    - novatune-network
```
- Remove `kafka` and `ncache` override sections
- Add Garnet dev settings if needed

## 1.5 Update .env.example
```env
# Messaging (Redpanda)
REDPANDA_BROKERS=localhost:19092
REDPANDA_SASL_ENABLED=false
REDPANDA_SASL_USERNAME=
REDPANDA_SASL_PASSWORD=
TOPIC_PREFIX=dev

# Cache (Garnet)
GARNET_CONNECTION=localhost:6379
GARNET_PASSWORD=
GARNET_SSL_ENABLED=false
```

## 1.6 Create Topic Initialization Script
Create `scripts/init-topics.sh`:
```bash
#!/bin/bash
TOPIC_PREFIX=${TOPIC_PREFIX:-dev}
rpk topic create "${TOPIC_PREFIX}-audio-events" --partitions 3 --config retention.ms=604800000
rpk topic create "${TOPIC_PREFIX}-track-deletions" --partitions 3 --config cleanup.policy=compact --config retention.ms=2592000000
```

---

## Verification
- [ ] `docker compose up -d` starts all services without errors
- [ ] `docker compose ps` shows all services healthy
- [ ] `rpk cluster info` returns cluster metadata
- [ ] `redis-cli -h localhost ping` returns PONG

**Exit Criteria:** All infrastructure services start and respond to health checks.
