# 12. Test Strategy

## Unit Tests

| Test Case | Description |
|-----------|-------------|
| ffprobe output parsing | Parse JSON output, handle malformed responses |
| Metadata validation | Duration limits, sample rate, channel count |
| Status transitions | Verify allowed/disallowed state changes |
| Idempotency | Replay scenarios for each track status |
| Waveform generation | Peak calculation, normalization, JSON output |
| DLQ message construction | Schema compliance, field population |
| Failure reason mapping | Exception → failure reason translation |
| Temp file cleanup | Verify cleanup in success/failure paths |

## Test Fixtures

```csharp
public static class TestAudioFiles
{
    public static readonly byte[] ValidMp3 = LoadResource("valid.mp3");
    public static readonly byte[] ValidFlac = LoadResource("valid.flac");
    public static readonly byte[] CorruptedFile = LoadResource("corrupted.bin");
    public static readonly byte[] TooLongTrack = LoadResource("3hour.mp3");
}

public static class FfprobeOutputs
{
    public const string ValidMp3Output = """
        {
          "format": {
            "duration": "180.5",
            "bit_rate": "320000"
          },
          "streams": [{
            "codec_name": "mp3",
            "sample_rate": "44100",
            "channels": 2
          }]
        }
        """;

    public const string MalformedOutput = "not valid json";

    public const string MissingFieldsOutput = """
        {
          "format": {},
          "streams": []
        }
        """;
}
```

## Test Cases by Component

### FfprobeService Tests

| Test | Input | Expected |
|------|-------|----------|
| `ParseValidMp3_ReturnsMetadata` | ValidMp3Output | AudioMetadata with Duration=180.5s |
| `ParseMalformed_ThrowsException` | MalformedOutput | FfprobeParseException |
| `ParseMissingFields_ThrowsException` | MissingFieldsOutput | FfprobeParseException |
| `Timeout_ThrowsTimeoutException` | Slow file | TimeoutException after 30s |

### MetadataValidator Tests

| Test | Input | Expected |
|------|-------|----------|
| `ValidMetadata_ReturnsSuccess` | Duration=3min | ValidationResult.Success |
| `DurationExceeded_ReturnsFailed` | Duration=3hr | DURATION_EXCEEDED |
| `ZeroDuration_ReturnsFailed` | Duration=0 | INVALID_DURATION |
| `InvalidChannels_ReturnsFailed` | Channels=0 | INVALID_CHANNELS |

### TrackStatusTransition Tests

| Test | From | To | Expected |
|------|------|-----|----------|
| `Processing_To_Ready_Allowed` | Processing | Ready | Success |
| `Processing_To_Failed_Allowed` | Processing | Failed | Success |
| `Ready_To_Processing_Blocked` | Ready | Processing | InvalidTransitionException |
| `Failed_To_Processing_Blocked` | Failed | Processing | InvalidTransitionException |

### IdempotencyHandler Tests

| Test | Track Status | Event | Expected |
|------|--------------|-------|----------|
| `AlreadyReady_SkipsProcessing` | Ready | AudioUploadedEvent | Skip, no state change |
| `AlreadyFailed_SkipsProcessing` | Failed | AudioUploadedEvent | Skip, no state change |
| `Deleted_SkipsProcessing` | Deleted | AudioUploadedEvent | Skip, no state change |
| `Processing_ExecutesHandler` | Processing | AudioUploadedEvent | Process normally |

## Mocking Strategy

```csharp
public interface IFfprobeService
{
    Task<AudioMetadata> ExtractMetadataAsync(string filePath, CancellationToken ct);
}

public interface IWaveformService
{
    Task<WaveformData> GenerateAsync(string filePath, int peakCount, CancellationToken ct);
}

// Unit tests use mocks
var ffprobeMock = Substitute.For<IFfprobeService>();
ffprobeMock.ExtractMetadataAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
    .Returns(expectedMetadata);
```

## Coverage Targets

| Component | Target |
|-----------|--------|
| FfprobeService | 90% |
| WaveformService | 85% |
| AudioProcessorHandler | 90% |
| MetadataValidator | 100% |
| StatusTransitionValidator | 100% |
