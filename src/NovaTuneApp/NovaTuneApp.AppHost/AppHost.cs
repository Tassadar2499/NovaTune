var builder = DistributedApplication.CreateBuilder(args);

// Cache (Garnet via Redis protocol)
var cache = builder.AddRedis("cache")
    .WithDataVolume("garnet-data");

// Messaging (Redpanda via Kafka protocol)
var messaging = builder.AddKafka("messaging")
    .WithDataVolume("redpanda-data");

var apiService = builder.AddProject<Projects.NovaTuneApp_ApiService>("apiservice")
    .WithReference(cache)
    .WithReference(messaging)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.NovaTuneApp_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WithReference(cache)
    .WaitFor(apiService);

builder.Build().Run();
