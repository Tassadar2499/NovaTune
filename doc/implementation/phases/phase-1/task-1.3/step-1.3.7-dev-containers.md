# Step 1.3.7: Configure JetBrains Rider Dev Containers

> **Parent Task:** [Task 1.3: Docker Compose Infrastructure](overview.md)
> **Status:** Pending
> **Output:** `.devcontainer/devcontainer.json`

## Objective

Configure a Dev Container environment for JetBrains Rider that provides a consistent, reproducible development environment. This ensures all team members have identical tooling and eliminates "works on my machine" issues.

## Background

Dev Containers (Development Containers) provide:

1. **Consistent environment** - Same OS, tools, and dependencies for all developers
2. **Isolated development** - No conflicts with host system packages
3. **Quick onboarding** - New developers get a working environment immediately
4. **Reproducibility** - Environment is defined as code

This configuration supports:
- **NF-8.1:** Development Environment standardization
- **NF-8.3:** Container Support requirements

## Prerequisites

Before setting up Dev Containers:

| Requirement | Minimum Version | How to Check |
|-------------|-----------------|--------------|
| JetBrains Rider | 2024.2+ | Help → About |
| Dev Containers Plugin | Latest | Settings → Plugins |
| Docker Desktop | 4.20+ | `docker --version` |
| Git | 2.x | `git --version` |

### Install Dev Containers Plugin

1. Open Rider
2. Go to **Settings/Preferences** → **Plugins**
3. Search for "Dev Containers"
4. Install and restart Rider

## Implementation Steps

### 1. Create Dev Container Configuration

Create `.devcontainer/devcontainer.json`:

```json
{
  "name": "NovaTune Dev",
  "image": "mcr.microsoft.com/devcontainers/dotnet:1-9.0-bookworm",

  "features": {
    "ghcr.io/devcontainers/features/docker-outside-of-docker:1": {},
    "ghcr.io/devcontainers/features/git:1": {
      "version": "latest",
      "ppa": true
    },
    "ghcr.io/devcontainers/features/github-cli:1": {},
    "ghcr.io/devcontainers/features/node:1": {
      "version": "lts"
    }
  },

  "remoteUser": "vscode",
  "updateRemoteUserUID": true,

  "mounts": [
    "source=${localEnv:HOME}/.nuget/packages,target=/home/vscode/.nuget/packages,type=bind,consistency=cached",
    "source=${localEnv:HOME}/.gitconfig,target=/home/vscode/.gitconfig,type=bind,readonly"
  ],

  "containerEnv": {
    "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
    "ASPNETCORE_ENVIRONMENT": "Development",
    "DOTNET_RUNNING_IN_CONTAINER": "true"
  },

  "postCreateCommand": "bash .devcontainer/post-create.sh",
  "postStartCommand": "bash .devcontainer/post-start.sh",

  "forwardPorts": [
    5000, 5001,
    8080,
    9000, 9001,
    9092,
    5672, 15672,
    8250, 9800,
    18888
  ],

  "portsAttributes": {
    "5000": { "label": "API HTTP", "onAutoForward": "notify" },
    "5001": { "label": "API HTTPS", "onAutoForward": "notify" },
    "8080": { "label": "RavenDB Studio", "onAutoForward": "silent" },
    "9000": { "label": "MinIO API", "onAutoForward": "silent" },
    "9001": { "label": "MinIO Console", "onAutoForward": "silent" },
    "9092": { "label": "Kafka", "onAutoForward": "silent" },
    "5672": { "label": "RabbitMQ AMQP", "onAutoForward": "silent" },
    "15672": { "label": "RabbitMQ Mgmt", "onAutoForward": "silent" },
    "8250": { "label": "NCache Mgmt", "onAutoForward": "silent" },
    "9800": { "label": "NCache Client", "onAutoForward": "silent" },
    "18888": { "label": "Aspire Dashboard", "onAutoForward": "notify" }
  },

  "customizations": {
    "jetbrains": {
      "ide": "Rider",
      "plugins": [
        "com.intellij.kubernetes"
      ]
    }
  },

  "hostRequirements": {
    "cpus": 4,
    "memory": "8gb",
    "storage": "32gb"
  }
}
```

### 2. Create Post-Create Script

Create `.devcontainer/post-create.sh`:

```bash
#!/bin/bash
# =============================================================================
# Dev Container Post-Create Script
# Runs once when the container is first created
# =============================================================================

set -e

echo "========================================"
echo " NovaTune Dev Container Setup"
echo "========================================"

# Display environment info
echo ""
echo "Environment:"
dotnet --info

# Restore NuGet packages
echo ""
echo "Restoring NuGet packages..."
dotnet restore src/NovaTuneApp/NovaTuneApp.sln

# Install global tools
echo ""
echo "Installing .NET global tools..."
dotnet tool install --global dotnet-ef || true
dotnet tool install --global dotnet-format || true
dotnet tool install --global dotnet-outdated-tool || true

# Add tools to PATH
export PATH="$PATH:/home/vscode/.dotnet/tools"

# Setup git hooks (if husky is configured)
if [ -f ".husky/install.mjs" ]; then
    echo ""
    echo "Setting up git hooks..."
    npm install
fi

# Create environment file if it doesn't exist
if [ ! -f ".env" ]; then
    echo ""
    echo "Creating .env from .env.example..."
    cp .env.example .env 2>/dev/null || echo "No .env.example found"
fi

echo ""
echo "========================================"
echo " Setup Complete!"
echo "========================================"
echo ""
echo "Next steps:"
echo "  1. Start infrastructure: docker compose up -d"
echo "  2. Wait for services: ./scripts/wait-for-services.sh"
echo "  3. Run the app: dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost"
echo ""
```

### 3. Create Post-Start Script

Create `.devcontainer/post-start.sh`:

```bash
#!/bin/bash
# =============================================================================
# Dev Container Post-Start Script
# Runs every time the container starts
# =============================================================================

echo "Dev container started at $(date)"

# Add .NET tools to PATH
export PATH="$PATH:/home/vscode/.dotnet/tools"

# Check if Docker is available
if docker info > /dev/null 2>&1; then
    echo "Docker is available"

    # Check infrastructure status
    if docker compose ps -q 2>/dev/null | grep -q .; then
        echo "Infrastructure containers are running"
        docker compose ps --format "table {{.Name}}\t{{.Status}}"
    else
        echo ""
        echo "Infrastructure is not running. Start with:"
        echo "  docker compose up -d"
    fi
else
    echo "Warning: Docker is not accessible. Check docker-outside-of-docker feature."
fi

echo ""
```

### 4. Make Scripts Executable

```bash
chmod +x .devcontainer/post-create.sh
chmod +x .devcontainer/post-start.sh
```

## Configuration Details

### Base Image

```json
"image": "mcr.microsoft.com/devcontainers/dotnet:1-9.0-bookworm"
```

| Component | Details |
|-----------|---------|
| Base OS | Debian Bookworm (12) |
| .NET SDK | 9.0 |
| User | `vscode` (non-root) |
| Shell | Bash + Zsh |

### Dev Container Features

| Feature | Purpose |
|---------|---------|
| `docker-outside-of-docker` | Run Docker commands from inside container |
| `git` | Latest Git with GPG signing support |
| `github-cli` | GitHub CLI for PR/issue management |
| `node` | Node.js LTS for frontend tooling |

### Volume Mounts

```json
"mounts": [
    "source=${localEnv:HOME}/.nuget/packages,target=/home/vscode/.nuget/packages,type=bind,consistency=cached",
    "source=${localEnv:HOME}/.gitconfig,target=/home/vscode/.gitconfig,type=bind,readonly"
]
```

| Mount | Purpose |
|-------|---------|
| `.nuget/packages` | Share NuGet cache with host (faster restores) |
| `.gitconfig` | Use host Git configuration |

## Opening in Rider

### Method 1: From Welcome Screen

1. Open JetBrains Rider
2. Click **Open** on the welcome screen
3. Select **Open Folder in Dev Container...**
4. Navigate to the NovaTune repository root
5. Rider builds and attaches to the container

### Method 2: From Tools Menu

1. Open the repository in Rider normally
2. Go to **Tools** → **Dev Containers** → **Open Folder in Dev Container...**
3. Select the repository root (detects `.devcontainer/devcontainer.json`)
4. Wait for container build and connection

### Method 3: Command Line

```bash
# Using Rider's command line launcher
rider devcontainer /path/to/NovaTune
```

## Working in the Dev Container

### Start Infrastructure

```bash
# From the terminal inside Rider (connected to dev container)
docker compose up -d

# Wait for services to be ready
./scripts/wait-for-services.sh

# Verify health
./scripts/healthcheck.sh
```

### Run the Application

```bash
# Run with Aspire orchestration
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost

# Or run API service directly
dotnet run --project src/NovaTuneApp/NovaTuneApp.ApiService
```

### Run Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## Troubleshooting

### Docker Permission Denied

**Symptom:** `permission denied while trying to connect to the Docker daemon`

**Solution:**
1. Restart the dev container
2. Or manually add user to docker group:
   ```bash
   sudo usermod -aG docker vscode
   newgrp docker
   ```

### Port Already in Use

**Symptom:** Container fails to start due to port conflict

**Solution:**
1. Stop conflicting services on host:
   ```bash
   # On host machine
   lsof -i :8080
   kill -9 <PID>
   ```
2. Or modify port mappings in `docker-compose.yml`

### Slow Performance (macOS/Windows)

**Symptom:** File operations are slow

**Solutions:**
1. Increase Docker Desktop resources (RAM/CPU)
2. Use named volumes instead of bind mounts for large directories
3. Enable VirtioFS file sharing in Docker Desktop

### Container Build Fails

**Symptom:** Dev container fails to build

**Solutions:**
1. Check Docker has enough disk space:
   ```bash
   docker system df
   ```
2. Clear Docker cache:
   ```bash
   docker system prune -a
   ```
3. Check internet connectivity for image pulls

### Environment Variables Not Loaded

**Symptom:** `.env` variables not available

**Solution:**
1. Verify `.env` file exists in repo root
2. Source it manually:
   ```bash
   export $(grep -v '^#' .env | xargs)
   ```

## Best Practices

### Keep Dev Container Lightweight

- Only install essential tools in `features`
- Use `postCreateCommand` for project-specific setup
- Cache NuGet packages via host mount

### Sync Configuration

- Mount `.gitconfig` from host for consistent Git settings
- Use `containerEnv` for development-specific variables
- Don't hardcode secrets in devcontainer.json

### Team Consistency

- Commit `.devcontainer/` to version control
- Document any manual setup steps
- Test dev container on fresh clone periodically

## Alternative: Dockerfile-based Container

For more control, use a custom Dockerfile:

```dockerfile
# .devcontainer/Dockerfile
FROM mcr.microsoft.com/devcontainers/dotnet:1-9.0-bookworm

# Install additional tools
RUN apt-get update && apt-get install -y \
    ffmpeg \
    && rm -rf /var/lib/apt/lists/*

# Install .NET tools
RUN dotnet tool install --global dotnet-ef
```

Update `devcontainer.json`:

```json
{
  "name": "NovaTune Dev",
  "build": {
    "dockerfile": "Dockerfile"
  },
  // ... rest of config
}
```

## Navigation

[Previous: Step 1.3.6 - Wait Scripts](step-1.3.6-wait-scripts.md) | [Overview](overview.md) | [Task 1.4: Aspire AppHost](../task-1.4-aspire-apphost.md)
