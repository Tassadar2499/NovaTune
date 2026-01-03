using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace NovaTuneApp.ApiService.Infrastructure.Configuration;

/// <summary>
/// Provides debug endpoints for configuration inspection.
/// Only available in development environments.
/// </summary>
public static class ConfigurationEndpoints
{
    /// <summary>
    /// Maps the /debug/config endpoint for development environments.
    /// Returns redacted configuration values for debugging purposes.
    /// </summary>
    public static IEndpointRouteBuilder MapDebugConfigEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/debug/config", (IOptions<NovaTuneOptions> options) =>
        {
            var config = options.Value;

            return Results.Ok(new
            {
                TopicPrefix = config.TopicPrefix,
                PresignedUrl = new
                {
                    TtlSeconds = config.PresignedUrl.TtlSeconds,
                    Ttl = config.PresignedUrl.Ttl.ToString()
                },
                CacheEncryption = new
                {
                    Enabled = config.CacheEncryption.Enabled,
                    // Redact the key - only show if present and length
                    KeyPresent = !string.IsNullOrEmpty(config.CacheEncryption.Key),
                    KeyLength = config.CacheEncryption.Key?.Length ?? 0,
                    Key = "[REDACTED]"
                },
                RateLimit = new
                {
                    config.RateLimit.RequestsPerMinute,
                    config.RateLimit.AnonymousRequestsPerMinute,
                    config.RateLimit.UploadsPerHour
                },
                Quotas = new
                {
                    MaxUploadSizeBytes = config.Quotas.MaxUploadSizeBytes,
                    MaxUploadSizeMB = config.Quotas.MaxUploadSizeBytes / (1024.0 * 1024.0),
                    config.Quotas.MaxPlaylistsPerUser,
                    config.Quotas.MaxTracksPerPlaylist,
                    MaxStoragePerUserBytes = config.Quotas.MaxStoragePerUserBytes,
                    MaxStoragePerUserGB = config.Quotas.MaxStoragePerUserBytes / (1024.0 * 1024.0 * 1024.0)
                }
            });
        })
        .WithName("GetDebugConfig")
        .WithTags("Debug")
        .ExcludeFromDescription(); // Don't show in OpenAPI docs

        return endpoints;
    }
}
