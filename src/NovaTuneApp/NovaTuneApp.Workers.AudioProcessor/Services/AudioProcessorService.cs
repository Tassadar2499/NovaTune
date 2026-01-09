using System.Diagnostics;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Infrastructure.Observability;
using NovaTuneApp.ApiService.Models;
using NovaTuneApp.ApiService.Services;
using Raven.Client.Documents;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions;

namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Implementation of audio processing service.
/// Handles metadata extraction, waveform generation, and track status updates.
/// Implements Req 3.1, 3.3, 3.4 per 02-processing-pipeline.md.
/// </summary>
public class AudioProcessorService : IAudioProcessorService
{
    private readonly IDocumentStore _documentStore;
    private readonly IStorageService _storageService;
    private readonly ITempFileManager _tempFileManager;
    private readonly IFfprobeService _ffprobeService;
    private readonly IWaveformService _waveformService;
    private readonly ILogger<AudioProcessorService> _logger;
    private readonly AudioProcessorOptions _options;

    public AudioProcessorService(
        IDocumentStore documentStore,
        IStorageService storageService,
        ITempFileManager tempFileManager,
        IFfprobeService ffprobeService,
        IWaveformService waveformService,
        ILogger<AudioProcessorService> logger,
        IOptions<AudioProcessorOptions> options)
    {
        _documentStore = documentStore;
        _storageService = storageService;
        _tempFileManager = tempFileManager;
        _ffprobeService = ffprobeService;
        _waveformService = waveformService;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<bool> ProcessAsync(AudioUploadedEvent @event, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var processingTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        processingTimeout.CancelAfter(TimeSpan.FromMinutes(_options.TotalProcessingTimeoutMinutes));
        var ct = processingTimeout.Token;

        _logger.LogInformation(
            "Starting audio processing for TrackId={TrackId}, CorrelationId={CorrelationId}",
            @event.TrackId,
            @event.CorrelationId);

        // Create temp directory for this track
        _tempFileManager.CreateTempDirectory(@event.TrackId);

        try
        {
            // Step 2: Load Track from RavenDB
            using var session = _documentStore.OpenAsyncSession();
            var track = await session.LoadAsync<Track>($"Tracks/{@event.TrackId}", ct);

            if (track is null)
            {
                _logger.LogWarning(
                    "Track {TrackId} not found in database - orphan event, acknowledging",
                    @event.TrackId);
                return true; // Ack the orphan message
            }

            if (track.Status != TrackStatus.Processing)
            {
                _logger.LogWarning(
                    "Track {TrackId} is in status {Status}, not Processing - already processed, acknowledging",
                    @event.TrackId,
                    track.Status);
                return true; // Already processed
            }

            // Step 3: Download audio from MinIO to temp storage (NF-2.4 streaming)
            var audioFileName = Path.GetFileName(@event.ObjectKey);
            var tempAudioPath = _tempFileManager.GetTempFilePath(@event.TrackId, audioFileName);

            _logger.LogDebug(
                "Downloading audio for TrackId={TrackId} from {ObjectKey} to {TempPath}",
                @event.TrackId, @event.ObjectKey, tempAudioPath);

            await _storageService.DownloadToFileAsync(@event.ObjectKey, tempAudioPath, ct);

            // Step 4: Run ffprobe to extract metadata
            _logger.LogDebug("Extracting metadata for TrackId={TrackId}", @event.TrackId);
            var metadata = await _ffprobeService.ExtractMetadataAsync(tempAudioPath, ct);

            // Validate metadata
            var validationResult = ValidateMetadata(metadata);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Metadata validation failed for TrackId={TrackId}: {Reason}",
                    @event.TrackId, validationResult.FailureReason);

                await MarkTrackFailedAsync(session, track, validationResult.FailureReason!, ct);
                stopwatch.Stop();
                NovaTuneMetrics.RecordAudioProcessingDuration(stopwatch.ElapsedMilliseconds);
                return true; // Ack - don't retry validation failures
            }

            // Step 5: Generate waveform using ffmpeg
            _logger.LogDebug("Generating waveform for TrackId={TrackId}", @event.TrackId);
            var waveformFileName = $"{@event.TrackId}.waveform.json";
            var tempWaveformPath = _tempFileManager.GetTempFilePath(@event.TrackId, waveformFileName);

            await _waveformService.GenerateAsync(
                tempAudioPath,
                tempWaveformPath,
                _options.WaveformPeakCount,
                ct);

            // Upload waveform to MinIO
            var waveformObjectKey = $"waveforms/{@event.TrackId}/{waveformFileName}";
            await _storageService.UploadFromFileAsync(
                waveformObjectKey,
                tempWaveformPath,
                "application/json",
                ct);

            _logger.LogDebug(
                "Uploaded waveform for TrackId={TrackId} to {ObjectKey}",
                @event.TrackId, waveformObjectKey);

            // Step 6: Update Track in RavenDB with optimistic concurrency
            track.Metadata = metadata;
            track.Duration = metadata.Duration;
            track.WaveformObjectKey = waveformObjectKey;
            track.Status = TrackStatus.Ready;
            track.ProcessedAt = DateTimeOffset.UtcNow;
            track.UpdatedAt = DateTimeOffset.UtcNow;

            // Enable optimistic concurrency for this track
            session.Advanced.UseOptimisticConcurrency = true;

            try
            {
                await session.SaveChangesAsync(ct);
            }
            catch (ConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict updating TrackId={TrackId} - will retry",
                    @event.TrackId);
                throw; // Retry the message
            }

            stopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingDuration(stopwatch.ElapsedMilliseconds);

            _logger.LogInformation(
                "Audio processing completed for TrackId={TrackId} in {ElapsedMs}ms",
                @event.TrackId,
                stopwatch.ElapsedMilliseconds);

            return true;
        }
        catch (OperationCanceledException) when (processingTimeout.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Audio processing timed out for TrackId={TrackId} after {Timeout} minutes",
                @event.TrackId,
                _options.TotalProcessingTimeoutMinutes);

            await MarkTrackFailedAsync(@event.TrackId, ProcessingFailureReason.ProcessingTimeout);
            stopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingDuration(stopwatch.ElapsedMilliseconds);
            return false;
        }
        catch (FfprobeException ex)
        {
            _logger.LogError(ex, "ffprobe failed for TrackId={TrackId}", @event.TrackId);
            await MarkTrackFailedAsync(@event.TrackId, ex.FailureReason);
            stopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingDuration(stopwatch.ElapsedMilliseconds);
            return true; // Don't retry ffprobe failures
        }
        catch (WaveformException ex)
        {
            _logger.LogError(ex, "Waveform generation failed for TrackId={TrackId}", @event.TrackId);
            await MarkTrackFailedAsync(@event.TrackId, ex.FailureReason);
            stopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingDuration(stopwatch.ElapsedMilliseconds);
            return true; // Don't retry waveform failures
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio processing failed for TrackId={TrackId}", @event.TrackId);
            throw; // Will be retried or sent to DLQ by handler
        }
        finally
        {
            // Step 7: Always clean up temp files
            _tempFileManager.CleanupTempDirectory(@event.TrackId);
        }
    }

    private ValidationResult ValidateMetadata(AudioMetadata metadata)
    {
        // Duration validation
        if (metadata.Duration <= TimeSpan.Zero)
        {
            return ValidationResult.Failed(ProcessingFailureReason.InvalidDuration);
        }

        if (metadata.Duration > TimeSpan.FromMinutes(_options.MaxTrackDurationMinutes))
        {
            return ValidationResult.Failed(ProcessingFailureReason.DurationExceeded);
        }

        // Sample rate validation
        if (metadata.SampleRate <= 0)
        {
            return ValidationResult.Failed(ProcessingFailureReason.InvalidSampleRate);
        }

        // Channel count validation (1-8)
        if (metadata.Channels < 1 || metadata.Channels > 8)
        {
            return ValidationResult.Failed(ProcessingFailureReason.InvalidChannels);
        }

        return ValidationResult.Success();
    }

    private async Task MarkTrackFailedAsync(IAsyncDocumentSession session, Track track, string failureReason, CancellationToken ct)
    {
        track.Status = TrackStatus.Failed;
        track.FailureReason = failureReason;
        track.ProcessedAt = DateTimeOffset.UtcNow;
        track.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await session.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Marked Track {TrackId} as Failed with reason {Reason}",
                track.Id, failureReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to mark Track {TrackId} as Failed",
                track.Id);
        }
    }

    private async Task MarkTrackFailedAsync(string trackId, string failureReason)
    {
        try
        {
            using var session = _documentStore.OpenAsyncSession();
            var track = await session.LoadAsync<Track>($"Tracks/{trackId}");

            if (track is not null)
            {
                track.Status = TrackStatus.Failed;
                track.FailureReason = failureReason;
                track.ProcessedAt = DateTimeOffset.UtcNow;
                track.UpdatedAt = DateTimeOffset.UtcNow;

                await session.SaveChangesAsync();
                _logger.LogInformation(
                    "Marked Track {TrackId} as Failed with reason {Reason}",
                    trackId, failureReason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to mark Track {TrackId} as Failed",
                trackId);
        }
    }

    private record ValidationResult(bool IsValid, string? FailureReason)
    {
        public static ValidationResult Success() => new(true, null);
        public static ValidationResult Failed(string reason) => new(false, reason);
    }
}
