namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Configuration options for Kafka/Redpanda messaging.
/// </summary>
public class KafkaOptions
{
    /// <summary>
    /// Comma-separated list of broker addresses.
    /// </summary>
    public string Brokers { get; set; } = "localhost:19092";

    /// <summary>
    /// Consumer group ID for this application.
    /// </summary>
    public string GroupId { get; set; } = "novatune-api";

    /// <summary>
    /// Client ID for producer identification.
    /// </summary>
    public string ClientId { get; set; } = "novatune-producer";

    /// <summary>
    /// Topic name prefix for environment isolation.
    /// </summary>
    public string TopicPrefix { get; set; } = "dev";

    /// <summary>
    /// Auto offset reset strategy (earliest, latest).
    /// </summary>
    public string AutoOffsetReset { get; set; } = "earliest";

    /// <summary>
    /// Enable SASL authentication.
    /// </summary>
    public bool SaslEnabled { get; set; } = false;

    /// <summary>
    /// SASL username (if enabled).
    /// </summary>
    public string? SaslUsername { get; set; }

    /// <summary>
    /// SASL password (if enabled).
    /// </summary>
    public string? SaslPassword { get; set; }
}
