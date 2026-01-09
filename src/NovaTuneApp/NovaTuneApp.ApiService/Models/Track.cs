using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models;

public sealed class Track
{
    /// <summary>
    /// RavenDB document ID (e.g., "Tracks/{TrackId}").
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// External track identifier (ULID).
    /// </summary>
    [Required]
    [MaxLength(26)]
    public string TrackId { get; init; } = string.Empty;

    /// <summary>
    /// User identifier (ULID).
    /// </summary>
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Artist { get; set; }

    public TimeSpan Duration { get; set; }

    [Required]
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// MIME type of the audio file.
    /// </summary>
    [MaxLength(64)]
    public string MimeType { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? Checksum { get; set; }

    public AudioMetadata? Metadata { get; set; }
    public TrackStatus Status { get; set; } = TrackStatus.Processing;

    /// <summary>
    /// MinIO object key for waveform data (JSON peaks).
    /// </summary>
    [MaxLength(512)]
    public string? WaveformObjectKey { get; set; }

    /// <summary>
    /// Failure reason code when Status = Failed.
    /// See <see cref="ProcessingFailureReason"/> for valid values.
    /// </summary>
    [MaxLength(64)]
    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Timestamp when processing completed (Status changed to Ready or Failed).
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; set; }
}
