# Task 1.4: Aspire AppHost Configuration

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P1 (Must-have)
> **Status:** Pending

## Description

Configure Dotnet Aspire orchestration for local development.

---

## Subtasks

### 1.4.1 Update AppHost Program.cs

- [ ] Update `NovaTuneApp.AppHost/Program.cs` with resource definitions:

```csharp
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// ===========================================
// External Resources (reference Docker Compose)
// ===========================================

var ravendb = builder.AddConnectionString("ravendb");
var minio = builder.AddConnectionString("minio");
var kafka = builder.AddConnectionString("kafka");
var rabbitmq = builder.AddConnectionString("rabbitmq");
var ncache = builder.AddConnectionString("ncache");

// ===========================================
// Application Services
// ===========================================

// API Service - Main backend
var apiService = builder.AddProject<NovaTuneApp_ApiService>("apiservice")
    .WithReference(ravendb)
    .WithReference(minio)
    .WithReference(kafka)
    .WithReference(rabbitmq)
    .WithReference(ncache)
    .WithExternalHttpEndpoints();

// Web Frontend - Blazor application
builder.AddProject<NovaTuneApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
```

---

### 1.4.2 Configure Development Connection Strings

- [ ] Update `NovaTuneApp.AppHost/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "ravendb": "http://localhost:8080",
    "minio": "http://localhost:9000",
    "kafka": "localhost:9092",
    "rabbitmq": "amqp://guest:guest@localhost:5672",
    "ncache": "localhost:9800"
  },
  "Parameters": {
    "RavenDb": {
      "Database": "NovaTune"
    },
    "MinIO": {
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin",
      "Bucket": "novatune-dev-audio"
    },
    "NCache": {
      "CacheName": "novatune-cache"
    }
  }
}
```

---

### 1.4.3 Configure Test Connection Strings

- [ ] Create `NovaTuneApp.AppHost/appsettings.Test.json` for test containers:

```json
{
  "ConnectionStrings": {
    "ravendb": "http://localhost:18080",
    "minio": "http://localhost:19000",
    "kafka": "localhost:19092",
    "rabbitmq": "amqp://guest:guest@localhost:15672",
    "ncache": "localhost:19800"
  },
  "Parameters": {
    "RavenDb": {
      "Database": "NovaTune_Test"
    },
    "MinIO": {
      "AccessKey": "testadmin",
      "SecretKey": "testadmin",
      "Bucket": "novatune-test-audio"
    },
    "NCache": {
      "CacheName": "novatune-test-cache"
    }
  }
}
```

---

### 1.4.4 Environment-Specific Configuration

- [ ] Set up environment-specific configuration transforms

**ApiService `appsettings.json`:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "RavenDb": {
    "Database": "NovaTune"
  },
  "MinIO": {
    "Bucket": "novatune-audio",
    "UseSSL": true
  },
  "Kafka": {
    "GroupId": "novatune-api",
    "Topics": {
      "TrackUploaded": "track-uploaded",
      "TrackProcessed": "track-processed",
      "UserActivity": "user-activity"
    }
  },
  "NCache": {
    "CacheName": "novatune-cache",
    "ExpirationMinutes": 8
  },
  "Jwt": {
    "Issuer": "https://novatune.local",
    "Audience": "novatune-api",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```

**ApiService `appsettings.Development.json`:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "MinIO": {
    "Bucket": "novatune-dev-audio",
    "UseSSL": false
  }
}
```

---

### 1.4.5 Verify Aspire Dashboard

- [ ] Verify Aspire dashboard shows all services

**Verification steps:**
1. Start Docker Compose infrastructure: `docker compose up -d`
2. Start Aspire: `dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost`
3. Open Aspire Dashboard (URL shown in console)
4. Verify all services appear:
   - `apiservice` - Running/Healthy
   - `webfrontend` - Running/Healthy
5. Verify traces and logs are visible
6. Check structured logs include correlation IDs

---

## Acceptance Criteria

- [ ] `dotnet run --project NovaTuneApp.AppHost` starts all services
- [ ] Aspire dashboard accessible and shows healthy services
- [ ] Connection strings properly resolved

---

## Verification Commands

```bash
# Start infrastructure
docker compose up -d

# Start Aspire host
dotnet run --project src/NovaTuneApp/NovaTuneApp.AppHost

# In another terminal, verify API is running
curl -s http://localhost:5000/health | jq .

# Verify connection to services
curl -s http://localhost:5000/ready | jq .
```

---

## Troubleshooting

### Connection String Not Resolved
Ensure `appsettings.Development.json` contains all connection strings and the `ASPNETCORE_ENVIRONMENT` is set to `Development`.

### Service Not Starting
Check Aspire dashboard logs for specific error messages. Common issues:
- Port conflicts (check with `lsof -i :5000`)
- Missing Docker containers (run `docker compose ps`)

### Dashboard Not Accessible
Default dashboard URL is shown in console output. If not visible, check if `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` is set.

---

## File Checklist

- [ ] `NovaTuneApp.AppHost/Program.cs`
- [ ] `NovaTuneApp.AppHost/appsettings.Development.json`
- [ ] `NovaTuneApp.AppHost/appsettings.Test.json`
- [ ] `NovaTuneApp.ApiService/appsettings.json`
- [ ] `NovaTuneApp.ApiService/appsettings.Development.json`

---

## Navigation

[Task 1.3: Docker Compose](task-1.3-docker-compose.md) | [Phase 1 Overview](overview.md) | [Task 1.5: ServiceDefaults](task-1.5-service-defaults.md)
