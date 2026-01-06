# Step 1: Create AspireAuthApiFactory

## Objective

Create a new factory class that uses Aspire testing infrastructure instead of mocked services.

## File

`src/integration_tests/NovaTuneApp.IntegrationTests/AspireAuthApiFactory.cs`

## Key Components

1. Use `DistributedApplicationTestingBuilder.CreateAsync<Projects.NovaTuneApp_AppHost>()` to build the Aspire app
2. Get `HttpClient` for the API service from the distributed app
3. Configure JWT settings for tests via environment variables
4. Set high rate limits for test stability

## Implementation

```csharp
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using NovaTuneApp.ApiService.Models.Identity;

namespace NovaTuneApp.Tests;

public class AspireAuthApiFactory : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private HttpClient _client = null!;
    private IDocumentStore _documentStore = null!;

    public HttpClient Client => _client;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.NovaTuneApp_AppHost>();

        // Configure test-specific settings (see Step 6.2)
        // ...

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Get the API service HTTP client
        _client = _app.CreateHttpClient("apiservice");

        // Get RavenDB connection for test utilities
        var connectionString = await _app.GetConnectionStringAsync("novatune");
        _documentStore = new DocumentStore
        {
            Urls = [connectionString],
            Database = "NovaTune"
        };
        _documentStore.Initialize();
    }

    public async Task DisposeAsync()
    {
        _documentStore?.Dispose();
        await _app.DisposeAsync();
    }
}
```

## Dependencies

- `Aspire.Hosting.Testing` (already in project)
- `RavenDB.Client` (need to add reference)
- Reference to `NovaTuneApp.AppHost` project (already present)

## Acceptance Criteria

- [ ] Class compiles without errors
- [ ] Can create DistributedApplication from AppHost
- [ ] Can obtain HttpClient for API service
- [ ] Can connect to RavenDB for test utilities
