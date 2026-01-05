using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Resilience scaffolding for NovaTune dependencies (NF-1.4).
/// Configures baseline resilience patterns: timeouts, circuit breakers, and bulkheads.
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
    /// Resilience pipeline for database operations (RavenDB).
    /// Timeout: 5s, suitable for document queries and writes.
    /// </summary>
    public const string DatabasePipeline = "database-resilience";

    /// <summary>
    /// Resilience pipeline for storage operations (MinIO/S3).
    /// Timeout: 10s, accommodates larger file operations.
    /// </summary>
    public const string StoragePipeline = "storage-resilience";

    // ============================================================================
    // Default Timeouts per Dependency Class
    // ============================================================================

    /// <summary>Cache operation timeout (500ms).</summary>
    public static readonly TimeSpan CacheTimeout = TimeSpan.FromMilliseconds(500);

    /// <summary>Database operation timeout (5 seconds).</summary>
    public static readonly TimeSpan DatabaseTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Storage operation timeout (10 seconds).</summary>
    public static readonly TimeSpan StorageTimeout = TimeSpan.FromSeconds(10);

    // ============================================================================
    // Circuit Breaker Defaults
    // ============================================================================
    // Conservative defaults suitable for Stage 0 scaffolding.
    // Fine-tune per endpoint/operation in later stages.
    // ============================================================================

    private const double FailureRatioThreshold = 0.5; // 50% failure rate triggers break
    private const int MinimumThroughput = 10; // Minimum calls before circuit evaluates
    private static readonly TimeSpan SamplingDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);

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
    /// Pipelines include timeout, circuit breaker, and bulkhead (concurrency limiter) strategies.
    /// </summary>
    public static TBuilder AddResiliencePipelines<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddResiliencePipeline(CachePipeline, pipelineBuilder =>
        {
            ConfigurePipeline(pipelineBuilder, CacheTimeout, CacheConcurrencyLimit, CacheQueueLimit, "Cache");
        });

        builder.Services.AddResiliencePipeline(DatabasePipeline, pipelineBuilder =>
        {
            ConfigurePipeline(pipelineBuilder, DatabaseTimeout, DatabaseConcurrencyLimit, DatabaseQueueLimit, "Database");
        });

        builder.Services.AddResiliencePipeline(StoragePipeline, pipelineBuilder =>
        {
            ConfigurePipeline(pipelineBuilder, StorageTimeout, StorageConcurrencyLimit, StorageQueueLimit, "Storage");
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
