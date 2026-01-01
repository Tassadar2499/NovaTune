using KafkaFlow;
using Microsoft.Extensions.Logging;

namespace NovaTuneApp.ApiService.Infrastructure.Messaging;

/// <summary>
/// Background service that starts and stops the KafkaFlow bus.
/// This allows the application to start serving requests before Kafka is fully connected.
/// </summary>
public class KafkaFlowHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KafkaFlowHostedService> _logger;
    private IKafkaBus? _kafkaBus;

    private const int MaxRetries = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public KafkaFlowHostedService(
        IServiceProvider serviceProvider,
        ILogger<KafkaFlowHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting KafkaFlow bus in background...");

        // Small delay to let the app start serving requests first
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        // Retry loop for Kafka connection
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _kafkaBus = _serviceProvider.CreateKafkaBus();
                await _kafkaBus.StartAsync(stoppingToken);
                _logger.LogInformation("KafkaFlow bus started successfully on attempt {Attempt}", attempt);

                // Keep running until cancellation
                await Task.Delay(Timeout.Infinite, stoppingToken);
                return;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("KafkaFlow bus stopping due to cancellation");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to start KafkaFlow bus (attempt {Attempt}/{MaxRetries})",
                    attempt,
                    MaxRetries);

                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelay, stoppingToken);
                }
                else
                {
                    _logger.LogError(ex, "Failed to start KafkaFlow bus after {MaxRetries} attempts", MaxRetries);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_kafkaBus is not null)
        {
            _logger.LogInformation("Stopping KafkaFlow bus...");
            await _kafkaBus.StopAsync();
            _logger.LogInformation("KafkaFlow bus stopped");
        }

        await base.StopAsync(cancellationToken);
    }
}
