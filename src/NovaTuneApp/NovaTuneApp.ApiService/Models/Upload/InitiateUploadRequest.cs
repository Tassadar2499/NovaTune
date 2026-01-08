using System.ComponentModel.DataAnnotations;

namespace NovaTuneApp.ApiService.Models.Upload;

/// <summary>
/// Request to initiate a direct upload to storage.
/// </summary>
public record InitiateUploadRequest(
    /// <summary>
    /// Original filename (used for title default).
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(255)]
    string FileName,

    /// <summary>
    /// MIME type (validated against allow-list).
    /// </summary>
    [Required]
    string MimeType,

    /// <summary>
    /// File size in bytes (validated against max size and quota).
    /// </summary>
    [Required]
    [Range(1, long.MaxValue)]
    long FileSizeBytes,

    /// <summary>
    /// Optional track title (defaults to filename without extension).
    /// </summary>
    [MaxLength(255)]
    string? Title = null,

    /// <summary>
    /// Optional artist name.
    /// </summary>
    [MaxLength(255)]
    string? Artist = null);
