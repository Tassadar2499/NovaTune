using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NovaTuneApp.Workers.AudioProcessor.HealthChecks;

/// <summary>
/// Health check that verifies ffmpeg is available on the system.
/// Per 08-health-checks.md.
/// </summary>
public class FfmpegHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "ffmpeg",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                var path = (await process.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
                return HealthCheckResult.Healthy($"ffmpeg found at: {path}");
            }

            return HealthCheckResult.Unhealthy("ffmpeg not found in PATH");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to check ffmpeg availability", ex);
        }
    }
}
