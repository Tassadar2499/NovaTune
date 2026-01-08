var builder = DistributedApplication.CreateBuilder(args);

static string? GetEnvironmentName(string[] appArgs)
{
    foreach (var arg in appArgs)
    {
        const string prefix = "--environment=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return arg[prefix.Length..];
        }
    }

    return Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
}

var environmentName = GetEnvironmentName(args) ?? "Production";
var isTesting = string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

// Cache (Garnet via Redis protocol)
var cache = builder.AddRedis("cache")
    .WithDataVolume("garnet-data")
    .WithPersistence(TimeSpan.FromSeconds(10), 5);

// Database (RavenDB - system of record)
var ravenServer = builder.AddRavenDB("ravendb")
    .WithDataVolume("ravendb-data");
var database = ravenServer.AddDatabase("novatune");

if (isTesting)
{
    // Integration tests only need the API + core persistence. Keeping the graph small makes startup
    // deterministic and avoids 3+ minute cold-starts (e.g., Kafka/MinIO image pulls).
    builder.AddProject<Projects.NovaTuneApp_ApiService>("apiservice")
        .WithReference(cache)
        .WaitFor(cache)
        .WithReference(database)
        .WaitFor(database)
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Testing")
        .WithEnvironment("DOTNET_ENVIRONMENT", "Testing")
        .WithEnvironment("NovaTune__CacheEncryption__Enabled", "false")
        .WithEnvironment("NovaTune__TopicPrefix", "testing")
        .WithEnvironment("Kafka__TopicPrefix", "test")
        .WithEnvironment("Features__MessagingEnabled", "false")
        .WithEnvironment("Features__StorageEnabled", "false")
        .WithHttpHealthCheck(path: "/health", endpointName: "http");
}
else
{
    // Messaging (Redpanda via Kafka protocol)
    var messaging = builder.AddKafka("messaging")
        .WithDataVolume("redpanda-data");

    // Redpanda Console - Admin UI for Kafka/Redpanda management
    builder.AddContainer("redpanda-console", "redpandadata/console", "v2.8.0")
        .WithHttpEndpoint(port: 8085, targetPort: 8080, name: "console")
        .WithEnvironment("KAFKA_BROKERS", "messaging:9092")
        .WaitFor(messaging);

    // Storage (MinIO - S3-compatible object storage)
    var minioUser = builder.AddParameter("minio-user");
    var minioPassword = builder.AddParameter("minio-password");

    var storage = builder.AddContainer("storage", "minio/minio")
        .WithVolume("minio-data", "/data")
        .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "api")
        .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "console")
        .WithEnvironment("MINIO_ROOT_USER", minioUser)
        .WithEnvironment("MINIO_ROOT_PASSWORD", minioPassword)
        .WithArgs("server", "/data", "--console-address", ":9001")
        .WithHttpHealthCheck("/minio/health/live", endpointName: "api");

    // MinIO bucket initialization
    builder.AddContainer("storage-init", "minio/mc")
        .WithReference(storage.GetEndpoint("api"))
        .WaitFor(storage)
        .WithEnvironment("MINIO_ROOT_USER", minioUser)
        .WithEnvironment("MINIO_ROOT_PASSWORD", minioPassword)
        .WithEntrypoint("/bin/sh")
        .WithArgs("-c", """
            until mc alias set minio http://storage:9000 $MINIO_ROOT_USER $MINIO_ROOT_PASSWORD; do
                echo 'Waiting for MinIO...'
                sleep 2
            done
            mc mb --ignore-existing minio/novatune-audio
            mc mb --ignore-existing minio/novatune-covers
            echo 'Buckets created successfully'
            """);

    var apiService = builder.AddProject<Projects.NovaTuneApp_ApiService>("apiservice")
        .WithReference(cache)
        .WaitFor(cache)
        .WithReference(messaging)
        .WaitFor(messaging)
        .WithReference(database)
        .WaitFor(database)
        .WithReference(storage.GetEndpoint("api"))
        .WaitFor(storage)
        .WithHttpHealthCheck("/health");

    builder.AddProject<Projects.NovaTuneApp_Web>("webfrontend")
        .WithExternalHttpEndpoints()
        .WithHttpHealthCheck("/health")
        .WithReference(apiService)
        .WithReference(cache)
        .WaitFor(apiService);
}

var app = builder.Build();
await app.RunAsync();

Console.WriteLine("App finished");
