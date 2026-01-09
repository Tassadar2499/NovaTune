namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Configuration options for the Audio Processor Worker.
/// Maps to appsettings.json AudioProcessor section.
/// </summary>
public class AudioProcessorOptions
{
    public const string SectionName = "AudioProcessor";

    /// <summary>
    /// Maximum concurrent processing tasks (NF-2.1).
    /// Default: 4
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;

    /// <summary>
    /// Retry attempts before DLQ.
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Backoff delays per retry in milliseconds.
    /// Default: [1000, 5000, 30000] (1s, 5s, 30s exponential backoff)
    /// </summary>
    public int[] RetryBackoffMs { get; set; } = [1000, 5000, 30000];

    /// <summary>
    /// Maximum allowed track duration in minutes.
    /// Tracks exceeding this will be rejected with DURATION_EXCEEDED.
    /// Default: 120 minutes
    /// </summary>
    public int MaxTrackDurationMinutes { get; set; } = 120;

    /// <summary>
    /// Temp file storage path for processing.
    /// Default: /tmp/novatune-processing
    /// </summary>
    public string TempDirectory { get; set; } = "/tmp/novatune-processing";

    /// <summary>
    /// Maximum temp disk usage in MB (NF-2.4).
    /// Default: 2048 MB
    /// </summary>
    public int MaxTempDiskSpaceMb { get; set; } = 2048;

    /// <summary>
    /// ffprobe execution timeout in seconds.
    /// Default: 30 seconds
    /// </summary>
    public int FfprobeTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// ffmpeg execution timeout in seconds.
    /// Default: 120 seconds
    /// </summary>
    public int FfmpegTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Total processing time limit in minutes.
    /// Default: 10 minutes
    /// </summary>
    public int TotalProcessingTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Number of peaks to generate for waveform.
    /// Default: 1000
    /// </summary>
    public int WaveformPeakCount { get; set; } = 1000;

    /// <summary>
    /// Enable gzip compression for waveform data.
    /// Default: true
    /// </summary>
    public bool WaveformCompressionEnabled { get; set; } = true;
}
