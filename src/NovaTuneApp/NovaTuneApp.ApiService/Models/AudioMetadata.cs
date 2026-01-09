namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Audio file metadata extracted via ffprobe during processing.
/// </summary>
public sealed record AudioMetadata
{
    /// <summary>
    /// Track duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Sample rate in Hz (e.g., 44100, 48000).
    /// </summary>
    public required int SampleRate { get; init; }

    /// <summary>
    /// Number of audio channels (1 = mono, 2 = stereo).
    /// </summary>
    public required int Channels { get; init; }

    /// <summary>
    /// Bit rate in bits per second.
    /// </summary>
    public required int BitRate { get; init; }

    /// <summary>
    /// Audio codec short name (e.g., "mp3", "flac", "aac").
    /// </summary>
    public required string Codec { get; init; }

    /// <summary>
    /// Audio codec long name (e.g., "MP3 (MPEG audio layer 3)").
    /// </summary>
    public required string CodecLongName { get; init; }

    /// <summary>
    /// Bit depth for lossless formats (16, 24, 32). Null for lossy formats.
    /// </summary>
    public int? BitDepth { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// MIME type of the audio file.
    /// </summary>
    public string? MimeType { get; init; }

    // Embedded metadata (optional, extracted from tags)

    /// <summary>
    /// Title extracted from audio file tags.
    /// </summary>
    public string? EmbeddedTitle { get; init; }

    /// <summary>
    /// Artist extracted from audio file tags.
    /// </summary>
    public string? EmbeddedArtist { get; init; }

    /// <summary>
    /// Album extracted from audio file tags.
    /// </summary>
    public string? EmbeddedAlbum { get; init; }

    /// <summary>
    /// Year extracted from audio file tags.
    /// </summary>
    public int? EmbeddedYear { get; init; }

    /// <summary>
    /// Genre extracted from audio file tags.
    /// </summary>
    public string? EmbeddedGenre { get; init; }
}
