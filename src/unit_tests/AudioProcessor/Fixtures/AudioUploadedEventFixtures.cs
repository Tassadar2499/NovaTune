using NovaTuneApp.ApiService.Infrastructure.Messaging.Messages;
using NovaTuneApp.ApiService.Models;

namespace NovaTune.UnitTests.AudioProcessor.Fixtures;

/// <summary>
/// Test fixtures for AudioUploadedEvent and related Track objects.
/// </summary>
public static class AudioUploadedEventFixtures
{
    /// <summary>
    /// Creates a valid AudioUploadedEvent for testing.
    /// </summary>
    public static AudioUploadedEvent CreateValid(string? trackId = null, string? userId = null)
    {
        trackId ??= Ulid.NewUlid().ToString();
        userId ??= Ulid.NewUlid().ToString();

        return new AudioUploadedEvent
        {
            TrackId = trackId,
            UserId = userId,
            ObjectKey = $"audio/{userId}/{trackId}/abc123def456",
            MimeType = "audio/mpeg",
            FileSizeBytes = 5_000_000,
            Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            CorrelationId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a Track in Processing status for testing.
    /// </summary>
    public static Track CreateProcessingTrack(string trackId, string userId) => new()
    {
        Id = $"Tracks/{trackId}",
        TrackId = trackId,
        UserId = userId,
        Title = "Test Track",
        ObjectKey = $"audio/{userId}/{trackId}/abc123def456",
        MimeType = "audio/mpeg",
        FileSizeBytes = 5_000_000,
        Status = TrackStatus.Processing,
        CreatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates a Track with Ready status.
    /// </summary>
    public static Track CreateReadyTrack(string trackId, string userId) => new()
    {
        Id = $"Tracks/{trackId}",
        TrackId = trackId,
        UserId = userId,
        Title = "Test Track",
        ObjectKey = $"audio/{userId}/{trackId}/abc123def456",
        MimeType = "audio/mpeg",
        FileSizeBytes = 5_000_000,
        Status = TrackStatus.Ready,
        CreatedAt = DateTimeOffset.UtcNow,
        ProcessedAt = DateTimeOffset.UtcNow,
        Metadata = CreateValidMetadata()
    };

    /// <summary>
    /// Creates a Track with Failed status.
    /// </summary>
    public static Track CreateFailedTrack(string trackId, string userId, string failureReason) => new()
    {
        Id = $"Tracks/{trackId}",
        TrackId = trackId,
        UserId = userId,
        Title = "Test Track",
        ObjectKey = $"audio/{userId}/{trackId}/abc123def456",
        MimeType = "audio/mpeg",
        FileSizeBytes = 5_000_000,
        Status = TrackStatus.Failed,
        FailureReason = failureReason,
        CreatedAt = DateTimeOffset.UtcNow,
        ProcessedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates a Track with Deleted status.
    /// </summary>
    public static Track CreateDeletedTrack(string trackId, string userId) => new()
    {
        Id = $"Tracks/{trackId}",
        TrackId = trackId,
        UserId = userId,
        Title = "Test Track",
        ObjectKey = $"audio/{userId}/{trackId}/abc123def456",
        MimeType = "audio/mpeg",
        FileSizeBytes = 5_000_000,
        Status = TrackStatus.Deleted,
        CreatedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Creates valid AudioMetadata for testing.
    /// </summary>
    public static AudioMetadata CreateValidMetadata() => new()
    {
        Duration = TimeSpan.FromMinutes(3),
        SampleRate = 44100,
        Channels = 2,
        BitRate = 320000,
        Codec = "mp3",
        CodecLongName = "MP3 (MPEG audio layer 3)",
        FileSizeBytes = 5_000_000,
        MimeType = "audio/mpeg"
    };

    /// <summary>
    /// Creates metadata with duration exceeding the limit.
    /// </summary>
    public static AudioMetadata CreateExcessiveDurationMetadata() => new()
    {
        Duration = TimeSpan.FromHours(3), // Exceeds 120 min default limit
        SampleRate = 44100,
        Channels = 2,
        BitRate = 320000,
        Codec = "mp3",
        CodecLongName = "MP3 (MPEG audio layer 3)"
    };

    /// <summary>
    /// Creates metadata with zero duration.
    /// </summary>
    public static AudioMetadata CreateZeroDurationMetadata() => new()
    {
        Duration = TimeSpan.Zero,
        SampleRate = 44100,
        Channels = 2,
        BitRate = 320000,
        Codec = "mp3",
        CodecLongName = "MP3 (MPEG audio layer 3)"
    };

    /// <summary>
    /// Creates metadata with invalid channel count.
    /// </summary>
    public static AudioMetadata CreateInvalidChannelsMetadata() => new()
    {
        Duration = TimeSpan.FromMinutes(3),
        SampleRate = 44100,
        Channels = 0, // Invalid
        BitRate = 320000,
        Codec = "mp3",
        CodecLongName = "MP3 (MPEG audio layer 3)"
    };

    /// <summary>
    /// Creates metadata with unsupported codec.
    /// </summary>
    public static AudioMetadata CreateUnsupportedCodecMetadata() => new()
    {
        Duration = TimeSpan.FromMinutes(3),
        SampleRate = 44100,
        Channels = 2,
        BitRate = 320000,
        Codec = "h264", // Video codec, not supported
        CodecLongName = "H.264 / AVC"
    };
}
