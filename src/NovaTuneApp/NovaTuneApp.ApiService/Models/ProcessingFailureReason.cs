namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Constants for track processing failure reasons.
/// Used when Track.Status = Failed to indicate the cause.
/// </summary>
public static class ProcessingFailureReason
{
    /// <summary>
    /// Track duration exceeds maximum allowed (MaxTrackDurationMinutes).
    /// </summary>
    public const string DurationExceeded = "DURATION_EXCEEDED";

    /// <summary>
    /// Track duration is zero or negative.
    /// </summary>
    public const string InvalidDuration = "INVALID_DURATION";

    /// <summary>
    /// Audio codec is not supported.
    /// </summary>
    public const string UnsupportedCodec = "UNSUPPORTED_CODEC";

    /// <summary>
    /// Audio file is corrupted or unreadable.
    /// </summary>
    public const string CorruptedFile = "CORRUPTED_FILE";

    /// <summary>
    /// ffprobe execution timed out.
    /// </summary>
    public const string FfprobeTimeout = "FFPROBE_TIMEOUT";

    /// <summary>
    /// ffmpeg execution timed out.
    /// </summary>
    public const string FfmpegTimeout = "FFMPEG_TIMEOUT";

    /// <summary>
    /// Error reading from or writing to object storage.
    /// </summary>
    public const string StorageError = "STORAGE_ERROR";

    /// <summary>
    /// Total processing time exceeded limit.
    /// </summary>
    public const string ProcessingTimeout = "PROCESSING_TIMEOUT";

    /// <summary>
    /// Invalid sample rate (must be > 0).
    /// </summary>
    public const string InvalidSampleRate = "INVALID_SAMPLE_RATE";

    /// <summary>
    /// Invalid channel count (must be 1-8).
    /// </summary>
    public const string InvalidChannels = "INVALID_CHANNELS";

    /// <summary>
    /// An unclassified error occurred.
    /// </summary>
    public const string UnknownError = "UNKNOWN_ERROR";
}
