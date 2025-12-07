# Task 1.8: Secrets Management

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P1 (Must-have)
> **Status:** Pending
> **NFR Reference:** NF-3.1

## Description

Configure secure secrets management for local development (NF-3.1).

---

## Subtasks

### 1.8.1 Initialize User Secrets

- [ ] Initialize user secrets for ApiService:

```bash
# Initialize user secrets
dotnet user-secrets init --project src/NovaTuneApp/NovaTuneApp.ApiService

# Set example secrets
dotnet user-secrets set "Jwt:SigningKey" "your-256-bit-secret-key-here-minimum-32-chars" \
  --project src/NovaTuneApp/NovaTuneApp.ApiService

dotnet user-secrets set "MinIO:AccessKey" "minioadmin" \
  --project src/NovaTuneApp/NovaTuneApp.ApiService

dotnet user-secrets set "MinIO:SecretKey" "minioadmin" \
  --project src/NovaTuneApp/NovaTuneApp.ApiService
```

**Verify initialization:**
```bash
# Check if UserSecretsId is in .csproj
grep UserSecretsId src/NovaTuneApp/NovaTuneApp.ApiService/NovaTuneApp.ApiService.csproj

# List all secrets
dotnet user-secrets list --project src/NovaTuneApp/NovaTuneApp.ApiService
```

---

### 1.8.2 Document Required Secrets

- [ ] Document required secrets

**Required Secrets:**

| Secret Key | Description | Required | Example |
|------------|-------------|----------|---------|
| `Jwt:SigningKey` | RS256 private key path or symmetric key | Yes | Path to PEM file or 256-bit key |
| `MinIO:AccessKey` | MinIO access key | Yes | `minioadmin` |
| `MinIO:SecretKey` | MinIO secret key | Yes | `minioadmin` |
| `RavenDb:CertificatePath` | Path to client certificate | Production | `/path/to/cert.pfx` |
| `RavenDb:CertificatePassword` | Certificate password | Production | (secure value) |
| `Kafka:SaslUsername` | Kafka SASL username | Production | `kafka-user` |
| `Kafka:SaslPassword` | Kafka SASL password | Production | (secure value) |
| `RabbitMQ:Password` | RabbitMQ password | Production | (secure value) |
| `NCache:LicenseKey` | NCache license key | Production | (license key) |

---

### 1.8.3 Create Secrets Template

- [ ] Create `secrets.json.example` template:

```json
{
  "Jwt": {
    "SigningKey": "your-256-bit-secret-key-minimum-32-characters-long",
    "SigningKeyPath": "./keys/signing.pem"
  },
  "MinIO": {
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin"
  },
  "RavenDb": {
    "CertificatePath": "",
    "CertificatePassword": ""
  },
  "Kafka": {
    "SaslUsername": "",
    "SaslPassword": ""
  },
  "RabbitMQ": {
    "Username": "guest",
    "Password": "guest"
  },
  "NCache": {
    "LicenseKey": ""
  },
  "ExternalApis": {
    "SomeApiKey": ""
  }
}
```

**Add to `.gitignore`:**
```
secrets.json
*.pem
*.pfx
```

---

### 1.8.4 Add Secrets Validation on Startup

- [ ] Add secrets validation on startup (fail fast if missing)

**Create validation class:**
```csharp
// Infrastructure/SecretsValidator.cs
namespace NovaTuneApp.ApiService.Infrastructure;

public static class SecretsValidator
{
    public static IHostApplicationBuilder ValidateSecrets(
        this IHostApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var errors = new List<string>();

        // JWT signing key is always required
        var jwtKey = config["Jwt:SigningKey"];
        var jwtKeyPath = config["Jwt:SigningKeyPath"];

        if (string.IsNullOrWhiteSpace(jwtKey) && string.IsNullOrWhiteSpace(jwtKeyPath))
        {
            errors.Add("Jwt:SigningKey or Jwt:SigningKeyPath must be configured");
        }
        else if (!string.IsNullOrWhiteSpace(jwtKeyPath) && !File.Exists(jwtKeyPath))
        {
            errors.Add($"JWT signing key file not found: {jwtKeyPath}");
        }
        else if (!string.IsNullOrWhiteSpace(jwtKey) && jwtKey.Length < 32)
        {
            errors.Add("Jwt:SigningKey must be at least 32 characters (256 bits)");
        }

        // MinIO credentials
        if (string.IsNullOrWhiteSpace(config["MinIO:AccessKey"]))
        {
            errors.Add("MinIO:AccessKey must be configured");
        }
        if (string.IsNullOrWhiteSpace(config["MinIO:SecretKey"]))
        {
            errors.Add("MinIO:SecretKey must be configured");
        }

        // Production-only validations
        var env = builder.Environment;
        if (env.IsProduction())
        {
            // RavenDB certificate in production
            if (string.IsNullOrWhiteSpace(config["RavenDb:CertificatePath"]))
            {
                errors.Add("RavenDb:CertificatePath required in production");
            }

            // Check for default credentials
            if (config["MinIO:AccessKey"] == "minioadmin")
            {
                errors.Add("MinIO using default credentials in production");
            }
            if (config["RabbitMQ:Password"] == "guest")
            {
                errors.Add("RabbitMQ using default credentials in production");
            }
        }

        if (errors.Count > 0)
        {
            var message = "Configuration validation failed:\n" +
                string.Join("\n", errors.Select(e => $"  - {e}"));
            throw new InvalidOperationException(message);
        }

        return builder;
    }
}
```

**Register in `Program.cs`:**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Validate secrets early (fail fast)
builder.ValidateSecrets();
```

---

### 1.8.5 Document Production Secrets Strategy

- [ ] Document production secrets strategy

**Production Secrets Strategy:**

#### Azure Key Vault (Recommended for Azure)
```csharp
// Add NuGet package: Azure.Extensions.AspNetCore.Configuration.Secrets

builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{vaultName}.vault.azure.net/"),
    new DefaultAzureCredential());
```

**Key Vault naming convention:**
```
novatune-jwt-signingkey
novatune-minio-accesskey
novatune-minio-secretkey
novatune-ravendb-certificate
```

#### AWS Secrets Manager
```csharp
// Add NuGet package: Amazon.Extensions.Configuration.SystemsManager

builder.Configuration.AddSecretsManager(configurator: options =>
{
    options.SecretFilter = entry => entry.Name.StartsWith("novatune/");
    options.KeyGenerator = (entry, key) => key.Replace("novatune/", "").Replace("/", ":");
});
```

**Secret naming convention:**
```
novatune/jwt/signingkey
novatune/minio/accesskey
novatune/minio/secretkey
```

#### Kubernetes Secrets
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: novatune-secrets
type: Opaque
data:
  Jwt__SigningKey: <base64-encoded-key>
  MinIO__AccessKey: <base64-encoded-value>
  MinIO__SecretKey: <base64-encoded-value>
```

**Mount as environment variables:**
```yaml
envFrom:
  - secretRef:
      name: novatune-secrets
```

#### Environment Variables Pattern
For any platform, secrets can be provided via environment variables using the double-underscore convention:
```bash
export Jwt__SigningKey="your-secret-key"
export MinIO__AccessKey="accesskey"
export MinIO__SecretKey="secretkey"
```

---

## Key Generation Script

**Generate JWT RS256 keys:**
```bash
#!/bin/bash
# scripts/generate-jwt-keys.sh

mkdir -p keys

# Generate private key
openssl genrsa -out keys/signing.pem 2048

# Generate public key
openssl rsa -in keys/signing.pem -pubout -out keys/signing.pub.pem

# Set permissions
chmod 600 keys/signing.pem
chmod 644 keys/signing.pub.pem

echo "Keys generated in ./keys/"
```

---

## Acceptance Criteria

- [ ] User secrets configured for local development
- [ ] Application fails fast on missing required secrets
- [ ] Documentation covers all required secrets
- [ ] Production secrets strategy documented
- [ ] Default credentials rejected in production

---

## Verification Commands

```bash
# Check user secrets are initialized
grep UserSecretsId src/NovaTuneApp/NovaTuneApp.ApiService/*.csproj

# List configured secrets
dotnet user-secrets list --project src/NovaTuneApp/NovaTuneApp.ApiService

# Test startup validation (should fail if secrets missing)
dotnet run --project src/NovaTuneApp/NovaTuneApp.ApiService

# Generate JWT keys
./scripts/generate-jwt-keys.sh
```

---

## File Checklist

- [ ] `NovaTuneApp.ApiService/NovaTuneApp.ApiService.csproj` (UserSecretsId added)
- [ ] `secrets.json.example`
- [ ] `Infrastructure/SecretsValidator.cs`
- [ ] `scripts/generate-jwt-keys.sh`
- [ ] `.gitignore` (updated to exclude secrets)
- [ ] `doc/deployment/secrets.md` (production secrets documentation)

---

## Navigation

[Task 1.7: API Foundation](task-1.7-api-foundation.md) | [Phase 1 Overview](overview.md) | [Task 1.9: FFmpeg Image](task-1.9-ffmpeg-image.md)
