using Microsoft.Extensions.Logging;

namespace NovaTuneApp.Tests;

/// <summary>
/// Aspire end-to-end integration tests.
/// </summary>
public class WebTests
{
    [Fact]
    [Trait("Category", "Aspire")]
    public async Task GetWebResourceRootReturnsOkStatusCode()
    {
    }
}
