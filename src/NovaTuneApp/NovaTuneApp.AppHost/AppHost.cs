var builder = DistributedApplication.CreateBuilder(args);

// Cache (Garnet via Redis protocol)
var cache = builder.AddRedis("cache")
    .WithDataVolume("garnet-data")
    .WithPersistence(TimeSpan.FromSeconds(10), 5);

// Messaging (Redpanda via Kafka protocol)
var messaging = builder.AddKafka("messaging")
    .WithDataVolume("redpanda-data");

// Database (RavenDB - system of record)
var ravenServer = builder.AddRavenDB("ravendb")
    .WithDataVolume("ravendb-data");
var database = ravenServer.AddDatabase("novatune");

// Storage (MinIO - S3-compatible object storage)
var minioUser = builder.AddParameter("minio-user", secret: true);
var minioPassword = builder.AddParameter("minio-password", secret: true);

var storage = builder.AddContainer("storage", "minio/minio")
    .WithVolume("minio-data", "/data")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "api")
    .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "console")
    .WithEnvironment("MINIO_ROOT_USER", minioUser)
    .WithEnvironment("MINIO_ROOT_PASSWORD", minioPassword)
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithHttpHealthCheck("/minio/health/live");

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

var app = builder.Build();
await app.RunAsync();

Console.WriteLine("App finished");
