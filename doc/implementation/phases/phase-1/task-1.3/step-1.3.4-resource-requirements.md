# Step 1.3.4: Document Resource Requirements

> **Parent Task:** [Task 1.3: Docker Compose Infrastructure](overview.md)
> **Status:** Pending
> **Output:** Documentation

## Objective

Document the minimum and recommended system resource requirements for running NovaTune's infrastructure stack locally. This ensures developers can verify their systems meet requirements before setup.

## System Requirements

### Minimum Requirements

| Resource | Minimum | Recommended | Notes |
|----------|---------|-------------|-------|
| **RAM** | 8 GB | 16 GB | 8 GB allows basic operation; 16 GB for comfortable development |
| **CPU** | 4 cores | 8 cores | More cores improve parallel container performance |
| **Disk Space** | 20 GB | 50 GB | Space for images, volumes, and test data |
| **Docker Memory** | 6 GB | 10 GB | Allocated to Docker Desktop |

### Operating System Support

| OS | Version | Status |
|----|---------|--------|
| macOS | 12.0+ (Monterey) | Fully supported |
| Windows | 10/11 (WSL2) | Fully supported |
| Linux | Ubuntu 22.04+, Debian 12+ | Fully supported |
| Linux | Other distributions | Community supported |

### Docker Requirements

| Component | Minimum Version | Recommended |
|-----------|-----------------|-------------|
| Docker Engine | 24.0 | Latest stable |
| Docker Compose | V2.20 | Latest stable |
| Docker Desktop | 4.20 | Latest stable |

**Check versions:**
```bash
docker --version
docker compose version
```

## Per-Service Memory Breakdown

### Base Memory Usage

| Service | Idle Memory | Active Memory | Peak Memory |
|---------|-------------|---------------|-------------|
| **RavenDB** | 512 MB | 1-2 GB | 3 GB |
| **MinIO** | 128 MB | 256-512 MB | 1 GB |
| **Kafka** | 256 MB | 512 MB-1 GB | 2 GB |
| **RabbitMQ** | 128 MB | 256-512 MB | 1 GB |
| **NCache** | 256 MB | 512 MB | 1 GB |
| **Total** | ~1.3 GB | ~3-5 GB | ~8 GB |

### Memory Usage by Scenario

#### Scenario 1: Minimal Development
Running only essential services for basic API development.

```bash
# Start only database and storage
docker compose up -d ravendb minio
```

| Services | Memory Usage |
|----------|--------------|
| RavenDB + MinIO | ~1-2 GB |

#### Scenario 2: Full Stack Development
Running all services for complete feature development.

```bash
# Start all services
docker compose up -d
```

| Services | Memory Usage |
|----------|--------------|
| All 5 services | ~4-6 GB |

#### Scenario 3: With Debug Tools
Running full stack plus debugging/monitoring tools.

```bash
# Start with debug profile
docker compose --profile debug up -d
```

| Services | Memory Usage |
|----------|--------------|
| All services + Kafka UI + Mailhog | ~5-7 GB |

## Disk Space Requirements

### Docker Images

| Image | Compressed Size | Extracted Size |
|-------|-----------------|----------------|
| `ravendb/ravendb:6.0-ubuntu-latest` | ~300 MB | ~800 MB |
| `minio/minio:latest` | ~100 MB | ~250 MB |
| `confluentinc/cp-kafka:7.5.0` | ~400 MB | ~900 MB |
| `rabbitmq:3-management` | ~150 MB | ~400 MB |
| `alachisoft/ncache:latest` | ~500 MB | ~1.2 GB |
| **Total Images** | ~1.5 GB | ~3.5 GB |

### Volume Storage (Estimates)

| Volume | Initial | After 1 Month Dev | After 6 Months |
|--------|---------|-------------------|----------------|
| `ravendb-data` | 50 MB | 500 MB - 2 GB | 2-10 GB |
| `minio-data` | 0 | 1-5 GB | 5-20 GB |
| `kafka-data` | 10 MB | 100 MB - 1 GB | 500 MB - 5 GB |
| `rabbitmq-data` | 10 MB | 50-200 MB | 100-500 MB |
| `ncache-data` | 50 MB | 100-500 MB | 200 MB - 1 GB |

### Recommended Disk Allocation

| Category | Space |
|----------|-------|
| Docker images | 5 GB |
| Docker volumes | 10 GB |
| Build cache | 3 GB |
| Buffer | 2 GB |
| **Total Recommended** | **20 GB** |

## CPU Usage Patterns

### Idle State

When services are running but not actively processing:

| Service | CPU Usage |
|---------|-----------|
| RavenDB | <1% |
| MinIO | <1% |
| Kafka | 1-2% |
| RabbitMQ | <1% |
| NCache | <1% |

### Active Development

During typical development with moderate load:

| Service | CPU Usage |
|---------|-----------|
| RavenDB | 5-15% |
| MinIO | 2-10% |
| Kafka | 5-10% |
| RabbitMQ | 2-5% |
| NCache | 2-5% |

### Integration Testing

Running full test suite with parallel tests:

| Service | CPU Usage |
|---------|-----------|
| RavenDB | 20-40% |
| MinIO | 10-30% |
| Kafka | 15-25% |
| RabbitMQ | 10-20% |
| NCache | 10-20% |

## Docker Desktop Configuration

### macOS / Windows

1. Open Docker Desktop
2. Go to **Settings** → **Resources**
3. Configure:

| Setting | Minimum | Recommended |
|---------|---------|-------------|
| CPUs | 4 | 6-8 |
| Memory | 6 GB | 8-10 GB |
| Swap | 1 GB | 2 GB |
| Disk image size | 40 GB | 64 GB |

### Linux (Native Docker)

Docker on Linux uses host resources directly. Ensure:

```bash
# Check available memory
free -h

# Check available disk
df -h /var/lib/docker
```

## Performance Optimization

### Reduce Memory Usage

1. **Start only needed services:**
   ```bash
   docker compose up -d ravendb minio
   ```

2. **Apply resource limits** (in `docker-compose.override.yml`):
   ```yaml
   services:
     kafka:
       deploy:
         resources:
           limits:
             memory: 768M
   ```

3. **Clean up unused resources:**
   ```bash
   docker system prune -a --volumes
   ```

### Improve Startup Time

1. **Pre-pull images:**
   ```bash
   docker compose pull
   ```

2. **Use SSD storage** for Docker data directory

3. **Enable BuildKit:**
   ```bash
   export DOCKER_BUILDKIT=1
   ```

## Troubleshooting Resource Issues

### Out of Memory

**Symptoms:** Containers restart, system becomes unresponsive

**Solutions:**
1. Increase Docker memory allocation
2. Stop unnecessary containers
3. Add swap space (Linux):
   ```bash
   sudo fallocate -l 4G /swapfile
   sudo chmod 600 /swapfile
   sudo mkswap /swapfile
   sudo swapon /swapfile
   ```

### Out of Disk Space

**Symptoms:** Container creation fails, builds fail

**Solutions:**
1. Remove unused images:
   ```bash
   docker image prune -a
   ```
2. Remove stopped containers:
   ```bash
   docker container prune
   ```
3. Remove unused volumes:
   ```bash
   docker volume prune
   ```
4. Check Docker disk usage:
   ```bash
   docker system df
   ```

### High CPU Usage

**Symptoms:** System lag, fan noise, battery drain

**Solutions:**
1. Check which container is using CPU:
   ```bash
   docker stats
   ```
2. Reduce Kafka log retention (in override):
   ```yaml
   kafka:
     environment:
       - KAFKA_LOG_RETENTION_HOURS=1
   ```
3. Stop unused services

## Monitoring Resource Usage

### Real-time Monitoring

```bash
# Container resource usage
docker stats

# Formatted output
docker stats --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}"
```

### Disk Usage Analysis

```bash
# Overall Docker disk usage
docker system df

# Detailed breakdown
docker system df -v

# Volume sizes
docker volume ls -q | xargs -I {} docker volume inspect {} --format '{{.Name}}: {{.Mountpoint}}'
```

## Navigation

[Previous: Step 1.3.3 - Environment Variables](step-1.3.3-environment-variables.md) | [Overview](overview.md) | [Next: Step 1.3.5 - Health Check Scripts](step-1.3.5-health-check-scripts.md)
