namespace NovaTuneApp.ApiService.Models;

public sealed record AudioMetadata
{
    public string Format { get; init; } = string.Empty;
    public int Bitrate { get; init; }
    public int SampleRate { get; init; }
    public int Channels { get; init; }
    public long FileSizeBytes { get; init; }
    public string? MimeType { get; init; }
}
