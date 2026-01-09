using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models;

public sealed class Track
{
    public string Id { get; init; } = string.Empty;

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
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}
