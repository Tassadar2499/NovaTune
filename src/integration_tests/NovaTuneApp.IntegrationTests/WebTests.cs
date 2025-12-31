using Microsoft.Extensions.Logging;

namespace NovaTuneApp.Tests;

/// <summary>
/// Aspire end-to-end integration tests.
/// These tests require Aspire DCP to be properly configured.
/// Set ASPIRE_TESTS_ENABLED=true to run these tests.
/// </summary>
public class WebTests
{
    [SkippableFact]
    [Trait("Category", "Aspire")]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
        // Skip unless explicitly enabled - Aspire DCP tests require specific infrastructure
        var aspireTestsEnabled = Environment.GetEnvironmentVariable("ASPIRE_TESTS_ENABLED");
        Skip.If(
            !string.Equals(aspireTestsEnabled, "true", StringComparison.OrdinalIgnoreCase),
            "Aspire tests are disabled. Set ASPIRE_TESTS_ENABLED=true to run.");

        // Arrange - use generous timeouts for container startup
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var cancellationToken = cts.Token;

        var appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.NovaTuneApp_AppHost>(cancellationToken);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddFilter("Aspire.", LogLevel.Warning);
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        // Wait for resources in dependency order
        await app.ResourceNotifications.WaitForResourceHealthyAsync("cache", cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("messaging", cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("apiservice", cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("webfrontend", cancellationToken);

        // Act
        var httpClient = app.CreateHttpClient("webfrontend");
        var response = await httpClient.GetAsync("/", cancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
