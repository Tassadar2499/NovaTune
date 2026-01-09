using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NovaTuneApp.Workers.AudioProcessor.Services;

namespace NovaTuneApp.Workers.AudioProcessor.HealthChecks;

/// <summary>
/// Health check that verifies the temp directory is writable.
/// Per 08-health-checks.md.
/// </summary>
public class TempDirectoryHealthCheck : IHealthCheck
{
    private readonly AudioProcessorOptions _options;

    public TempDirectoryHealthCheck(IOptions<AudioProcessorOptions> options)
    {
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tempDir = _options.TempDirectory;

            // Ensure directory exists
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            // Test write capability
            var testFile = Path.Combine(tempDir, $".health-check-{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "health-check");
            File.Delete(testFile);

            return Task.FromResult(
                HealthCheckResult.Healthy($"Temp directory writable: {tempDir}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy($"Temp directory not writable: {_options.TempDirectory}", ex));
        }
    }
}
