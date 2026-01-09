# 3. Metadata Extraction (ffprobe)

## ffprobe Command

```bash
ffprobe -v quiet -print_format json -show_format -show_streams "{input_file}"
```

## AudioMetadata Schema

```csharp
public sealed class AudioMetadata
{
    public required TimeSpan Duration { get; init; }
    public required int SampleRate { get; init; }        // e.g., 44100, 48000
    public required int Channels { get; init; }          // 1 = mono, 2 = stereo
    public required int BitRate { get; init; }           // bits per second
    public required string Codec { get; init; }          // e.g., "mp3", "flac", "aac"
    public required string CodecLongName { get; init; }  // e.g., "MP3 (MPEG audio layer 3)"
    public int? BitDepth { get; init; }                  // For lossless formats (16, 24, 32)

    // Embedded metadata (optional, extracted from tags)
    public string? EmbeddedTitle { get; init; }
    public string? EmbeddedArtist { get; init; }
    public string? EmbeddedAlbum { get; init; }
    public int? EmbeddedYear { get; init; }
    public string? EmbeddedGenre { get; init; }
}
```

## Validation Rules (NF-2.4)

| Field | Rule | Action on Failure |
|-------|------|-------------------|
| `Duration` | ≤ `MaxTrackDuration` (default: 2 hours) | Mark Track `Failed` |
| `Duration` | > 0 | Mark Track `Failed` |
| `SampleRate` | > 0 | Mark Track `Failed` |
| `Channels` | 1–8 | Mark Track `Failed` |
| Codec | Recognized audio codec | Mark Track `Failed` |

## Failure Reasons

```csharp
public static class ProcessingFailureReason
{
    public const string DurationExceeded = "DURATION_EXCEEDED";
    public const string InvalidDuration = "INVALID_DURATION";
    public const string UnsupportedCodec = "UNSUPPORTED_CODEC";
    public const string CorruptedFile = "CORRUPTED_FILE";
    public const string FfprobeTimeout = "FFPROBE_TIMEOUT";
    public const string FfmpegTimeout = "FFMPEG_TIMEOUT";
    public const string StorageError = "STORAGE_ERROR";
    public const string UnknownError = "UNKNOWN_ERROR";
}
```
