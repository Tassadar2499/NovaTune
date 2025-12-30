# Phase 2: NuGet Package Updates

## 2.1 Remove Packages
From `NovaTuneApp.ApiService.csproj` and related projects:
```xml
<!-- Remove -->
<PackageReference Include="Alachisoft.NCache.SDK" />
<PackageReference Include="Alachisoft.NCache.SessionServices" />
<!-- Any RabbitMQ packages if present -->
<PackageReference Include="RabbitMQ.Client" />
```

## 2.2 Add Packages
In `NovaTuneApp.ApiService.csproj`:
```xml
<!-- Cache -->
<PackageReference Include="StackExchange.Redis" Version="2.8.16" />

<!-- KafkaFlow (replaces raw Confluent.Kafka) -->
<PackageReference Include="KafkaFlow" Version="3.0.10" />
<PackageReference Include="KafkaFlow.Microsoft.DependencyInjection" Version="3.0.10" />
<PackageReference Include="KafkaFlow.Serializer.JsonCore" Version="3.0.10" />
<PackageReference Include="KafkaFlow.Admin" Version="3.0.10" />
```

## 2.3 Aspire Hosting Updates
In `NovaTuneApp.AppHost.csproj`:
```xml
<!-- Redis hosting (works with Garnet) -->
<PackageReference Include="Aspire.Hosting.Redis" Version="9.0.0" />
<!-- Kafka hosting works with Redpanda -->
<PackageReference Include="Aspire.Hosting.Kafka" Version="9.0.0" />
```

In `NovaTuneApp.ServiceDefaults.csproj`:
```xml
<PackageReference Include="Aspire.StackExchange.Redis" Version="9.0.0" />
```

## 2.4 Remove Legacy Packages (if present)
```xml
<!-- Remove if exists -->
<PackageReference Include="Confluent.Kafka" />
```
Note: KafkaFlow includes Confluent.Kafka as a transitive dependency; no need to reference directly.

---

## Verification
- [ ] `dotnet restore` succeeds
- [ ] `dotnet build` succeeds with no NCache/RabbitMQ reference errors

**Exit Criteria:** Solution builds without deprecated package references.
