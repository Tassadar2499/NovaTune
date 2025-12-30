# Docker Infrastructure Skill

Manage Docker-based infrastructure for NovaTune development.

## Docker Compose Files

- `docker-compose.yml` - Main infrastructure services
- `docker-compose.override.yml` - Development overrides and debug tools

## Services

| Service | Port(s) | Purpose |
|---------|---------|---------|
| RavenDB | 8080 | Document database |
| MinIO | 9000, 9001 (console) | S3-compatible object storage |
| Redpanda | 19092 (Kafka API), 18082 (REST), 9644 (Admin) | Event streaming |
| Garnet | 6379 | Redis-compatible cache |

## Common Commands

```bash
# Start all infrastructure
docker compose up -d

# Start specific services
docker compose up -d ravendb minio

# Start with debug tools (redpanda-console, etc.)
docker compose --profile debug up -d

# Stop all services
docker compose down

# Stop and remove volumes (clean slate)
docker compose down -v

# View logs
docker compose logs -f
docker compose logs -f redpanda

# Check service health
docker compose ps
```

## Service Verification

```bash
# Check Redpanda cluster
docker exec novatune-redpanda rpk cluster info

# List Redpanda topics
docker exec novatune-redpanda rpk topic list

# Check Garnet (Redis)
docker exec novatune-garnet redis-cli ping

# Access MinIO console
# Browser: http://localhost:9001
# Default credentials: minioadmin / minioadmin

# Access RavenDB Studio
# Browser: http://localhost:8080
```

## Topic Management

```bash
# Create topics manually
docker exec novatune-redpanda rpk topic create dev-audio-events --partitions 3
docker exec novatune-redpanda rpk topic create dev-track-deletions --partitions 3 --config cleanup.policy=compact

# Delete topic
docker exec novatune-redpanda rpk topic delete dev-audio-events

# Describe topic
docker exec novatune-redpanda rpk topic describe dev-audio-events
```

## Troubleshooting

```bash
# Check container resource usage
docker stats

# Inspect container
docker inspect novatune-redpanda

# Shell into container
docker exec -it novatune-redpanda bash
docker exec -it novatune-garnet sh

# Reset a specific service
docker compose stop redpanda && docker compose rm -f redpanda
docker volume rm novatune_redpanda-data
docker compose up -d redpanda
```
