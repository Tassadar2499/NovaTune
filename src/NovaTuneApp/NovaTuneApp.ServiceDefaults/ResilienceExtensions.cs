using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Resilience scaffolding for NovaTune dependencies (NF-1.4).
/// Configures baseline resilience patterns: timeouts, circuit breakers, retries, and bulkheads.
/// </summary>
public static class ResilienceExtensions
{
    // ============================================================================
    // Resilience Pipeline Names
    // ============================================================================
    // Use these names to resolve pipelines from the DI container via:
    // ResiliencePipelineProvider<string>.GetPipeline(PipelineName)
    // ============================================================================

    /// <summary>
    /// Resilience pipeline for cache operations (Redis/Garnet).
    /// Timeout: 500ms, optimized for fast cache lookups.
    /// </summary>
    public const string CachePipeline = "cache-resilience";

    /// <summary>
    /// Resilience pipeline for database read operations (RavenDB).
    /// Timeout: 2s, 1 retry per NF-1.4 spec.
    /// </summary>
    public const string DatabaseReadPipeline = "database-read-resilience";

    /// <summary>
    /// Resilience pipeline for database write operations (RavenDB).
    /// Timeout: 5s, 0 retries (idempotent via outbox) per NF-1.4 spec.
    /// </summary>
    public const string DatabaseWritePipeline = "database-write-resilience";

    /// <summary>
    /// Resilience pipeline for database operations (RavenDB).
    /// Timeout: 5s, suitable for document queries and writes.
    /// </summary>
    [Obsolete("Use DatabaseReadPipeline or DatabaseWritePipeline for stage 2+ code")]
    public const string DatabasePipeline = "database-resilience";

    /// <summary>
    /// Resilience pipeline for storage presign operations (MinIO/S3).
    /// Timeout: 5s, 1 retry per NF-1.4 spec.
    /// </summary>
    public const string StoragePresignPipeline = "storage-presign-resilience";

    /// <summary>
    /// Resilience pipeline for storage operations (MinIO/S3).
    /// Timeout: 10s, accommodates larger file operations.
    /// </summary>
    public const string StoragePipeline = "storage-resilience";

    /// <summary>
    /// Resilience pipeline for messaging operations (Redpanda/Kafka).
    /// Timeout: 2s, 2 retries per NF-1.4 spec.
    /// </summary>
    public const string MessagingPipeline = "messaging-resilience";

    // ============================================================================
    // Default Timeouts per Dependency Class (NF-1.4)
    // ============================================================================

    /// <summary>Cache operation timeout (500ms).</summary>
    public static readonly TimeSpan CacheTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>Database read operation timeout (2 seconds) per NF-1.4.</summary>
    public static readonly TimeSpan DatabaseReadTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Database write operation timeout (5 seconds) per NF-1.4.</summary>
    public static readonly TimeSpan DatabaseWriteTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Database operation timeout (5 seconds).</summary>
    [Obsolete("Use DatabaseReadTimeout or DatabaseWriteTimeout for stage 2+ code")]
    public static readonly TimeSpan DatabaseTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Storage presign operation timeout (5 seconds) per NF-1.4.</summary>
    public static readonly TimeSpan StoragePresignTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Storage operation timeout (10 seconds).</summary>
    public static readonly TimeSpan StorageTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Messaging operation timeout (2 seconds) per NF-1.4.</summary>
    public static readonly TimeSpan MessagingTimeout = TimeSpan.FromSeconds(2);

    // ============================================================================
    // Circuit Breaker Defaults (NF-1.4)
    // ============================================================================
    // Per spec: Open after 5 consecutive failures, half-open after 30s.
    // ============================================================================

    private const int ConsecutiveFailureThreshold = 5; // 5 consecutive failures triggers break
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);

    // Legacy defaults for backward compatibility
    private const double FailureRatioThreshold = 0.5; // 50% failure rate triggers break
    private const int MinimumThroughput = 10; // Minimum calls before circuit evaluates
    private static readonly TimeSpan SamplingDuration = TimeSpan.FromSeconds(30);

    // ============================================================================
    // Bulkhead / Concurrency Limiter Defaults
    // ============================================================================
    // Limits concurrent operations to prevent resource exhaustion.
    // ============================================================================

    private const int CacheConcurrencyLimit = 100;
    private const int CacheQueueLimit = 50;

    private const int DatabaseConcurrencyLimit = 50;
    private const int DatabaseQueueLimit = 25;

    private const int StorageConcurrencyLimit = 20;
    private const int StorageQueueLimit = 10;

    /// <summary>
    /// Adds resilience pipelines for all dependency classes to the service collection.
    /// Pipelines include timeout, circuit breaker, retry, and bulkhead (concurrency limiter) strategies.
    /// </summary>
    public static TBuilder AddResiliencePipelines<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddResiliencePipeline(CachePipeline, pipelineBuilder =>
        {
            ConfigurePipeline(pipelineBuilder, CacheTimeout, CacheConcurrencyLimit, CacheQueueLimit, "Cache");
        });

        // Legacy database pipeline for backward compatibility
#pragma warning disable CS0618 // Type or member is obsolete
        builder.Services.AddResiliencePipeline(DatabasePipeline, pipelineBuilder =>
        {
            ConfigurePipeline(pipelineBuilder, DatabaseTimeout, DatabaseConcurrencyLimit, DatabaseQueueLimit, "Database");
        });
#pragma warning restore CS0618

        // Database read pipeline: 2s timeout, 1 retry (NF-1.4)
        builder.Services.AddResiliencePipeline(DatabaseReadPipeline, pipelineBuilder =>
        {
            ConfigurePipelineWithRetry(
                pipelineBuilder,
                timeout: DatabaseReadTimeout,
                concurrencyLimit: DatabaseConcurrencyLimit,
                queueLimit: DatabaseQueueLimit,
                maxRetries: 1,
                dependencyName: "DatabaseRead");
        });

        // Database write pipeline: 5s timeout, 0 retries (NF-1.4)
        builder.Services.AddResiliencePipeline(DatabaseWritePipeline, pipelineBuilder =>
        {
            ConfigurePipelineWithRetry(
                pipelineBuilder,
                timeout: DatabaseWriteTimeout,
                concurrencyLimit: DatabaseConcurrencyLimit,
                queueLimit: DatabaseQueueLimit,
                maxRetries: 0,
                dependencyName: "DatabaseWrite");
        });

        builder.Services.AddResiliencePipeline(StoragePipeline, pipelineBuilder =>
        {
            ConfigurePipeline(pipelineBuilder, StorageTimeout, StorageConcurrencyLimit, StorageQueueLimit, "Storage");
        });

        // Storage presign pipeline: 5s timeout, 1 retry (NF-1.4)
        builder.Services.AddResiliencePipeline(StoragePresignPipeline, pipelineBuilder =>
        {
            ConfigurePipelineWithRetry(
                pipelineBuilder,
                timeout: StoragePresignTimeout,
                concurrencyLimit: StorageConcurrencyLimit,
                queueLimit: StorageQueueLimit,
                maxRetries: 1,
                dependencyName: "StoragePresign");
        });

        // Messaging pipeline: 2s timeout, 2 retries (NF-1.4)
        builder.Services.AddResiliencePipeline(MessagingPipeline, pipelineBuilder =>
        {
            ConfigurePipelineWithRetry(
                pipelineBuilder,
                timeout: MessagingTimeout,
                concurrencyLimit: DatabaseConcurrencyLimit, // Use database limits for messaging
                queueLimit: DatabaseQueueLimit,
                maxRetries: 2,
                dependencyName: "Messaging");
        });

        return builder;
    }

    private static void ConfigurePipeline(
        ResiliencePipelineBuilder builder,
        TimeSpan timeout,
        int concurrencyLimit,
        int queueLimit,
        string dependencyName)
    {
        // Order matters: Bulkhead -> Circuit Breaker -> Timeout
        // - Bulkhead first to limit concurrent requests entering the pipeline
        // - Circuit breaker to fail fast when dependency is unhealthy
        // - Timeout as innermost to cancel individual operations

        // Bulkhead (Concurrency Limiter)
        builder.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = concurrencyLimit,
            QueueLimit = queueLimit
        });

        // Circuit Breaker
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = FailureRatioThreshold,
            MinimumThroughput = MinimumThroughput,
            SamplingDuration = SamplingDuration,
            BreakDuration = BreakDuration,
            ShouldHandle = new PredicateBuilder()
                .Handle<Exception>()
                .Handle<TimeoutRejectedException>(),
            OnOpened = args =>
            {
                // Circuit opened - dependency is considered unhealthy
                // Logging handled by telemetry enrichment
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                // Circuit closed - dependency recovered
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                // Circuit half-open - testing if dependency recovered
                return ValueTask.CompletedTask;
            }
        });

        // Timeout
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = timeout,
            OnTimeout = args =>
            {
                // Timeout occurred - operation took too long
                return ValueTask.CompletedTask;
            }
        });
    }

    /// <summary>
    /// Configures a resilience pipeline with NF-1.4 compliant circuit breaker (consecutive failures)
    /// and optional retry support.
    /// </summary>
    private static void ConfigurePipelineWithRetry(
        ResiliencePipelineBuilder builder,
        TimeSpan timeout,
        int concurrencyLimit,
        int queueLimit,
        int maxRetries,
        string dependencyName)
    {
        // Order matters: Bulkhead -> Retry -> Circuit Breaker -> Timeout
        // - Bulkhead first to limit concurrent requests entering the pipeline
        // - Retry wraps circuit breaker so retries don't count as circuit breaker failures
        // - Circuit breaker to fail fast when dependency is unhealthy
        // - Timeout as innermost to cancel individual operations

        // Bulkhead (Concurrency Limiter)
        builder.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = concurrencyLimit,
            QueueLimit = queueLimit
        });

        // Retry (only if maxRetries > 0)
        if (maxRetries > 0)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                MaxDelay = TimeSpan.FromSeconds(2),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>()
                    .Handle<TimeoutRejectedException>()
            });
        }

        // Circuit Breaker - NF-1.4 spec: 5 consecutive failures, 30s half-open
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            // Use consecutive failure strategy for NF-1.4 compliance
            FailureRatio = 1.0, // 100% - effectively consecutive failures
            MinimumThroughput = ConsecutiveFailureThreshold,
            SamplingDuration = TimeSpan.FromSeconds(10), // Short window for consecutive tracking
            BreakDuration = BreakDuration,
            ShouldHandle = new PredicateBuilder()
                .Handle<Exception>()
                .Handle<TimeoutRejectedException>(),
            OnOpened = args =>
            {
                // Circuit opened - dependency is considered unhealthy
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                // Circuit closed - dependency recovered
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                // Circuit half-open - testing if dependency recovered
                return ValueTask.CompletedTask;
            }
        });

        // Timeout
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = timeout,
            OnTimeout = args =>
            {
                // Timeout occurred - operation took too long
                return ValueTask.CompletedTask;
            }
        });
    }

    /// <summary>
    /// Adds resilience pipelines with generic TResult support for typed operations.
    /// Use this when you need to execute operations that return specific types.
    /// </summary>
    public static TBuilder AddTypedResiliencePipelines<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddResiliencePipeline<string, object>(CachePipeline, pipelineBuilder =>
        {
            ConfigureTypedPipeline(pipelineBuilder, CacheTimeout, CacheConcurrencyLimit, CacheQueueLimit, "Cache");
        });

        builder.Services.AddResiliencePipeline<string, object>(DatabasePipeline, pipelineBuilder =>
        {
            ConfigureTypedPipeline(pipelineBuilder, DatabaseTimeout, DatabaseConcurrencyLimit, DatabaseQueueLimit, "Database");
        });

        builder.Services.AddResiliencePipeline<string, object>(StoragePipeline, pipelineBuilder =>
        {
            ConfigureTypedPipeline(pipelineBuilder, StorageTimeout, StorageConcurrencyLimit, StorageQueueLimit, "Storage");
        });

        return builder;
    }

    private static void ConfigureTypedPipeline<TResult>(
        ResiliencePipelineBuilder<TResult> builder,
        TimeSpan timeout,
        int concurrencyLimit,
        int queueLimit,
        string dependencyName)
    {
        // Bulkhead (Concurrency Limiter)
        builder.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = concurrencyLimit,
            QueueLimit = queueLimit
        });

        // Circuit Breaker
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<TResult>
        {
            FailureRatio = FailureRatioThreshold,
            MinimumThroughput = MinimumThroughput,
            SamplingDuration = SamplingDuration,
            BreakDuration = BreakDuration,
            ShouldHandle = new PredicateBuilder<TResult>()
                .Handle<Exception>()
                .Handle<TimeoutRejectedException>()
        });

        // Timeout
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = timeout
        });
    }
}
