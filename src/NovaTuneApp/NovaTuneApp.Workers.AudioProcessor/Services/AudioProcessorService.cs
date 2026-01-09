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
            "Starting audio processing for TrackId={TrackId}, UserId={UserId}, CorrelationId={CorrelationId}",
            @event.TrackId,
            @event.UserId,
            @event.CorrelationId);

        // Step 1: Check disk space before processing (10-resilience.md)
        if (!_tempFileManager.HasSufficientDiskSpace())
        {
            _logger.LogWarning(
                "Processing failed for TrackId={TrackId}, FailureReason={FailureReason}, CorrelationId={CorrelationId}",
                @event.TrackId,
                ProcessingFailureReason.DiskSpaceExceeded,
                @event.CorrelationId);

            NovaTuneMetrics.RecordAudioProcessingFailed(ProcessingFailureReason.DiskSpaceExceeded);
            // Return false to trigger retry - disk space may become available
            return false;
        }

        // Create temp directory for this track
        _tempFileManager.CreateTempDirectory(@event.TrackId);

        try
        {
            // Step 2: Load Track from RavenDB
            using var session = _documentStore.OpenAsyncSession();
            var track = await session.LoadAsync<Track>($"Tracks/{@event.TrackId}", ct);

            if (track is null)
            {
                _logger.LogError(
                    "Track {TrackId} not found in database, CorrelationId={CorrelationId}",
                    @event.TrackId,
                    @event.CorrelationId);
                NovaTuneMetrics.RecordAudioProcessingSkipped("not_found");
                return true; // Ack the orphan message
            }

            if (track.Status != TrackStatus.Processing)
            {
                _logger.LogWarning(
                    "Track {TrackId} is in status {Status}, not Processing - skipping, CorrelationId={CorrelationId}",
                    @event.TrackId,
                    track.Status,
                    @event.CorrelationId);
                NovaTuneMetrics.RecordAudioProcessingSkipped("already_processed");
                return true; // Already processed
            }

            // Step 3: Download audio from MinIO to temp storage (NF-2.4 streaming)
            var audioFileName = Path.GetFileName(@event.ObjectKey);
            var tempAudioPath = _tempFileManager.GetTempFilePath(@event.TrackId, audioFileName);

            // NF-4.5: Redact ObjectKey - only log filename, not full path with user ID
            _logger.LogDebug(
                "Downloading audio for TrackId={TrackId}, FileName={FileName}",
                @event.TrackId, audioFileName);

            var downloadStopwatch = Stopwatch.StartNew();
            using (var downloadSpan = NovaTuneMetrics.StartAudioDownloadSpan(@event.TrackId, @event.CorrelationId))
            {
                // Use 5-minute timeout for large audio files up to 500 MB (10-resilience.md)
                await _storageService.DownloadLargeFileAsync(@event.ObjectKey, tempAudioPath, ct);
            }
            downloadStopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingStageDuration("download", downloadStopwatch.Elapsed.TotalSeconds);

            // Step 4: Run ffprobe to extract metadata
            AudioMetadata metadata;
            var ffprobeStopwatch = Stopwatch.StartNew();
            using (var ffprobeSpan = NovaTuneMetrics.StartFfprobeSpan(@event.TrackId, @event.CorrelationId))
            {
                metadata = await _ffprobeService.ExtractMetadataAsync(tempAudioPath, ct);
            }
            ffprobeStopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingStageDuration("ffprobe", ffprobeStopwatch.Elapsed.TotalSeconds);

            // Log metadata extraction per 09-observability.md
            _logger.LogDebug(
                "Metadata extracted for TrackId={TrackId}, Duration={Duration}, Codec={Codec}, SampleRate={SampleRate}",
                @event.TrackId,
                metadata.Duration,
                metadata.Codec,
                metadata.SampleRate);

            // Validate metadata
            var validationResult = ValidateMetadata(metadata);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Processing failed for TrackId={TrackId}, FailureReason={FailureReason}, CorrelationId={CorrelationId}",
                    @event.TrackId,
                    validationResult.FailureReason,
                    @event.CorrelationId);

                await MarkTrackFailedAsync(session, track, validationResult.FailureReason!, ct);
                stopwatch.Stop();
                NovaTuneMetrics.RecordAudioProcessingDuration(stopwatch.ElapsedMilliseconds);
                NovaTuneMetrics.RecordAudioProcessingFailed(validationResult.FailureReason);
                return true; // Ack - don't retry validation failures
            }

            // Step 5: Generate waveform using ffmpeg
            const string waveformFileName = "peaks.json";
            var tempWaveformPath = _tempFileManager.GetTempFilePath(@event.TrackId, waveformFileName);

            var waveformStopwatch = Stopwatch.StartNew();
            using (var waveformSpan = NovaTuneMetrics.StartWaveformSpan(@event.TrackId, @event.CorrelationId))
            {
                await _waveformService.GenerateAsync(
                    tempAudioPath,
                    tempWaveformPath,
                    _options.WaveformPeakCount,
                    ct);
            }
            waveformStopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingStageDuration("waveform", waveformStopwatch.Elapsed.TotalSeconds);

            // Get waveform file size for logging
            var waveformSize = new FileInfo(tempWaveformPath).Length;
            _logger.LogDebug(
                "Waveform generated for TrackId={TrackId}, WaveformSize={WaveformSize}",
                @event.TrackId,
                waveformSize);

            // Upload waveform to MinIO per 04-waveform-generation.md: waveforms/{userId}/{trackId}/peaks.json
            var waveformObjectKey = $"waveforms/{@event.UserId}/{@event.TrackId}/{waveformFileName}";
            await _storageService.UploadFromFileAsync(
                waveformObjectKey,
                tempWaveformPath,
                "application/json",
                ct);

            // Step 6: Update Track in RavenDB with optimistic concurrency
            track.Metadata = metadata;
            track.Duration = metadata.Duration;
            track.WaveformObjectKey = waveformObjectKey;
            track.Status = TrackStatus.Ready;
            track.ProcessedAt = DateTimeOffset.UtcNow;
            track.UpdatedAt = DateTimeOffset.UtcNow;

            // Enable optimistic concurrency for this track
            session.Advanced.UseOptimisticConcurrency = true;

            var persistStopwatch = Stopwatch.StartNew();
            using (var persistSpan = NovaTuneMetrics.StartPersistSpan(@event.TrackId, @event.CorrelationId))
            {
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
            }
            persistStopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingStageDuration("persist", persistStopwatch.Elapsed.TotalSeconds);

            stopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingDuration(stopwatch.ElapsedMilliseconds);

            // Record audio track content duration
            NovaTuneMetrics.RecordAudioTrackDuration(metadata.Duration.TotalSeconds);

            _logger.LogInformation(
                "Audio processing completed for TrackId={TrackId}, DurationMs={DurationMs}, CorrelationId={CorrelationId}",
                @event.TrackId,
                stopwatch.ElapsedMilliseconds,
                @event.CorrelationId);

            return true;
        }
        catch (OperationCanceledException) when (processingTimeout.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Processing failed for TrackId={TrackId}, FailureReason={FailureReason}, CorrelationId={CorrelationId}",
                @event.TrackId,
                ProcessingFailureReason.ProcessingTimeout,
                @event.CorrelationId);

            await MarkTrackFailedAsync(@event.TrackId, ProcessingFailureReason.ProcessingTimeout);
            stopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingDuration(stopwatch.ElapsedMilliseconds);
            NovaTuneMetrics.RecordAudioProcessingFailed(ProcessingFailureReason.ProcessingTimeout);
            return false;
        }
        catch (FfprobeException ex)
        {
            _logger.LogWarning(
                ex,
                "Processing failed for TrackId={TrackId}, FailureReason={FailureReason}, CorrelationId={CorrelationId}",
                @event.TrackId,
                ex.FailureReason,
                @event.CorrelationId);
            await MarkTrackFailedAsync(@event.TrackId, ex.FailureReason);
            stopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingDuration(stopwatch.ElapsedMilliseconds);
            NovaTuneMetrics.RecordAudioProcessingFailed(ex.FailureReason);
            return true; // Don't retry ffprobe failures
        }
        catch (WaveformException ex)
        {
            _logger.LogWarning(
                ex,
                "Processing failed for TrackId={TrackId}, FailureReason={FailureReason}, CorrelationId={CorrelationId}",
                @event.TrackId,
                ex.FailureReason,
                @event.CorrelationId);
            await MarkTrackFailedAsync(@event.TrackId, ex.FailureReason);
            stopwatch.Stop();
            NovaTuneMetrics.RecordAudioProcessingDuration(stopwatch.ElapsedMilliseconds);
            NovaTuneMetrics.RecordAudioProcessingFailed(ex.FailureReason);
            return true; // Don't retry waveform failures
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Processing failed for TrackId={TrackId}, FailureReason={FailureReason}, CorrelationId={CorrelationId}",
                @event.TrackId,
                "transient_error",
                @event.CorrelationId);
            throw; // Will be retried or sent to DLQ by handler
        }
        finally
        {
            // Step 7: Always clean up temp files
            _tempFileManager.CleanupTempDirectory(@event.TrackId);
        }
    }

    // Supported audio codecs per NF-2.4
    private static readonly HashSet<string> SupportedCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "aac", "flac", "vorbis", "opus", "alac",
        "wav", "pcm_s16le", "pcm_s24le", "pcm_s32le", "pcm_f32le"
    };

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

        // Codec validation - must be a recognized audio codec
        if (string.IsNullOrEmpty(metadata.Codec) || !SupportedCodecs.Contains(metadata.Codec))
        {
            return ValidationResult.Failed(ProcessingFailureReason.UnsupportedCodec);
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
