using System.Diagnostics;

namespace NovaTuneApp.ApiService.Infrastructure.Observability;

/// <summary>
/// Provides instrumentation helpers for authentication operations.
/// Reduces boilerplate for stopwatch/metrics/activity tracking.
/// </summary>
public static class AuthInstrumentation
{
    /// <summary>
    /// Executes an async operation with full instrumentation (activity tracing, duration metrics, operation counters).
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operationName">Name of the operation (e.g., "Register", "Login").</param>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="configureActivity">Optional action to add tags to the activity.</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<T> ExecuteWithInstrumentationAsync<T>(
        string operationName,
        Func<Task<T>> operation,
        Action<Activity?>? configureActivity = null)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = NovaTuneActivitySource.Source.StartActivity($"AuthService.{operationName}");
        configureActivity?.Invoke(activity);

        try
        {
            var result = await operation();
            NovaTuneMetrics.IncrementAuthOperation(operationName.ToLowerInvariant(), "success");
            return result;
        }
        catch (Exception)
        {
            // Metrics for failures are recorded by the caller with specific error types
            throw;
        }
        finally
        {
            stopwatch.Stop();
            NovaTuneMetrics.RecordAuthDuration(operationName.ToLowerInvariant(), stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Executes a void async operation with full instrumentation.
    /// </summary>
    /// <param name="operationName">Name of the operation.</param>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="configureActivity">Optional action to add tags to the activity.</param>
    public static async Task ExecuteWithInstrumentationAsync(
        string operationName,
        Func<Task> operation,
        Action<Activity?>? configureActivity = null)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = NovaTuneActivitySource.Source.StartActivity($"AuthService.{operationName}");
        configureActivity?.Invoke(activity);

        try
        {
            await operation();
            NovaTuneMetrics.IncrementAuthOperation(operationName.ToLowerInvariant(), "success");
        }
        finally
        {
            stopwatch.Stop();
            NovaTuneMetrics.RecordAuthDuration(operationName.ToLowerInvariant(), stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
