# 12. Test Strategy

This document defines the testing approach for Stage 3 (Audio Processor Worker), following the established patterns from Stages 1 and 2.

## Testing Framework and Conventions

| Tool | Purpose |
|------|---------|
| xUnit | Test framework |
| Shouldly | Fluent assertions |
| BaseTest | DI container integration with fakes |
| Fakes | In-memory test doubles with state tracking |

### Naming Conventions

- Test classes: `{Component}Tests` (e.g., `FfprobeServiceTests`, `AudioProcessorServiceTests`)
- Test methods: `Should_{expected_behavior}` or `{Method}_Should_{expected_behavior}_{when_condition}`
- Fakes: `{Interface}Fake` (e.g., `StorageServiceFake`, `DocumentStoreFake`)

### Test Organization

Tests are organized by component with clear section headers:

```csharp
public class AudioProcessorServiceTests : BaseTest
{
    // ============================================================================
    // Metadata Extraction Tests
    // ============================================================================

    [Fact]
    public void Should_extract_metadata_from_valid_audio()
    {
        // ...
    }

    // ============================================================================
    // Status Transition Tests
    // ============================================================================

    [Fact]
    public void Should_update_track_status_to_ready_on_success()
    {
        // ...
    }
}
```

## Unit Test Cases

### FfprobeService Tests

| Test | Description | Expected |
|------|-------------|----------|
| `Should_parse_valid_mp3_metadata` | Parse ffprobe JSON for MP3 | AudioMetadata with correct Duration, Codec, SampleRate |
| `Should_parse_valid_flac_metadata` | Parse ffprobe JSON for FLAC | AudioMetadata with lossless codec info |
| `Should_throw_for_malformed_json` | Invalid JSON response | FfprobeException with CorruptedFile reason |
| `Should_throw_for_missing_streams` | Empty streams array | FfprobeException with CorruptedFile reason |
| `Should_throw_for_missing_duration` | No duration field | FfprobeException with InvalidDuration reason |
| `Should_throw_on_timeout` | Process exceeds 30s | FfprobeException with FfprobeTimeout reason |
| `Should_extract_all_metadata_fields` | Complete ffprobe output | All AudioMetadata fields populated |

### Metadata Validation Tests

| Test | Input | Expected |
|------|-------|----------|
| `Should_accept_valid_metadata` | Duration=3min, Channels=2, SampleRate=44100 | ValidationResult.Success |
| `Should_reject_zero_duration` | Duration=0 | INVALID_DURATION |
| `Should_reject_negative_duration` | Duration=-1 | INVALID_DURATION |
| `Should_reject_duration_exceeding_limit` | Duration=3hr (> MaxTrackDurationMinutes) | DURATION_EXCEEDED |
| `Should_reject_zero_sample_rate` | SampleRate=0 | INVALID_SAMPLE_RATE |
| `Should_reject_zero_channels` | Channels=0 | INVALID_CHANNELS |
| `Should_reject_excessive_channels` | Channels=10 (> 8) | INVALID_CHANNELS |
| `Should_reject_unsupported_codec` | Codec="video_h264" | UNSUPPORTED_CODEC |
| `Should_accept_all_supported_codecs` | mp3, aac, flac, vorbis, opus, alac, wav | ValidationResult.Success |

### WaveformService Tests

| Test | Description | Expected |
|------|-------------|----------|
| `Should_generate_correct_peak_count` | WaveformPeakCount=1000 | JSON with 1000 peaks |
| `Should_normalize_peaks_to_range` | Any audio | Peak values between -1.0 and 1.0 |
| `Should_throw_on_timeout` | Process exceeds 120s | WaveformException with FfmpegTimeout reason |
| `Should_produce_valid_json_output` | Valid audio | Parseable JSON array |
| `Should_handle_mono_audio` | Mono file | Single channel peaks |
| `Should_handle_stereo_audio` | Stereo file | Stereo peaks (L/R or mixed) |

### TempFileManager Tests

| Test | Description | Expected |
|------|-------------|----------|
| `Should_create_temp_directory_for_track` | CreateTempDirectory("track-1") | Directory exists at {TempDirectory}/track-1 |
| `Should_cleanup_temp_directory` | CleanupTempDirectory("track-1") | Directory deleted |
| `Should_return_correct_temp_file_path` | GetTempFilePath("track-1", "audio.mp3") | {TempDirectory}/track-1/audio.mp3 |
| `Should_detect_disk_space_exceeded` | CurrentUsage > MaxTempDiskSpaceMb | HasSufficientDiskSpace() returns false |
| `Should_cleanup_orphaned_directories` | Directories older than 1 hour | Old directories removed |
| `Should_tolerate_missing_directory_on_cleanup` | CleanupTempDirectory for non-existent | No exception, logs debug |

### AudioProcessorService Tests

| Test | Description | Expected |
|------|-------------|----------|
| `Should_process_valid_audio_and_update_track` | Valid AudioUploadedEvent | Track.Status = Ready, Metadata populated |
| `Should_skip_if_track_not_found` | TrackId doesn't exist | Return true (ack), skip processing |
| `Should_skip_if_track_already_processed` | Track.Status = Ready | Return true, RecordSkipped("already_processed") |
| `Should_mark_failed_on_validation_error` | Invalid metadata | Track.Status = Failed, FailureReason set |
| `Should_fail_fast_on_disk_space_exceeded` | HasSufficientDiskSpace = false | Return false (retry), RecordFailed(DiskSpaceExceeded) |
| `Should_cleanup_temp_files_on_success` | Successful processing | Temp directory removed |
| `Should_cleanup_temp_files_on_failure` | Processing fails | Temp directory removed |
| `Should_upload_waveform_to_correct_path` | Successful processing | waveforms/{userId}/{trackId}/peaks.json |
| `Should_handle_concurrency_exception` | SaveChanges throws | Re-throw for retry |

### DLQ Handler Tests

| Test | Description | Expected |
|------|-------------|----------|
| `Should_construct_dlq_message_with_all_fields` | Failed processing | OriginalMessage, FailureReason, FailedAt, RetryCount |
| `Should_publish_to_dlq_topic` | HandleFailedMessage called | Message sent to {prefix}-audio-events-dlq |
| `Should_include_exception_details` | Exception occurred | ExceptionType, ExceptionMessage in DLQ message |

### IdempotencyHandler Tests

| Test | Track Status | Event | Expected |
|------|--------------|-------|----------|
| `Should_skip_ready_track` | Ready | AudioUploadedEvent | Return true, RecordSkipped |
| `Should_skip_failed_track` | Failed | AudioUploadedEvent | Return true, RecordSkipped |
| `Should_skip_deleted_track` | Deleted | AudioUploadedEvent | Return true, RecordSkipped |
| `Should_process_processing_track` | Processing | AudioUploadedEvent | Process normally |

## Test Fakes

Following the established pattern, fakes are in-memory implementations with state tracking and configurable callbacks.

### StorageServiceFake

```csharp
public class StorageServiceFake : IStorageService
{
    public Dictionary<string, byte[]> Objects { get; } = new();
    public List<string> DownloadedKeys { get; } = new();
    public List<(string Key, string Path)> UploadedFiles { get; } = new();

    public Func<string, string, Task>? OnDownloadLargeFileAsync { get; set; }
    public Func<string, string, string, Task>? OnUploadFromFileAsync { get; set; }

    public async Task DownloadLargeFileAsync(string objectKey, string destinationPath, CancellationToken ct)
    {
        if (OnDownloadLargeFileAsync != null)
        {
            await OnDownloadLargeFileAsync(objectKey, destinationPath);
            return;
        }

        DownloadedKeys.Add(objectKey);
        if (Objects.TryGetValue(objectKey, out var data))
        {
            await File.WriteAllBytesAsync(destinationPath, data, ct);
        }
    }

    public async Task UploadFromFileAsync(string objectKey, string sourcePath, string contentType, CancellationToken ct)
    {
        if (OnUploadFromFileAsync != null)
        {
            await OnUploadFromFileAsync(objectKey, sourcePath, contentType);
            return;
        }

        UploadedFiles.Add((objectKey, sourcePath));
        Objects[objectKey] = await File.ReadAllBytesAsync(sourcePath, ct);
    }
}
```

### DocumentStoreFake

```csharp
public class DocumentStoreFake
{
    public Dictionary<string, Track> Tracks { get; } = new();

    public Func<string, Track?>? OnLoadTrack { get; set; }
    public Action<Track>? OnSaveTrack { get; set; }
    public bool ThrowConcurrencyOnSave { get; set; }

    public Task<Track?> LoadAsync(string id, CancellationToken ct)
    {
        if (OnLoadTrack != null)
        {
            return Task.FromResult(OnLoadTrack(id));
        }

        Tracks.TryGetValue(id, out var track);
        return Task.FromResult(track);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        if (ThrowConcurrencyOnSave)
        {
            throw new ConcurrencyException("Simulated concurrency conflict");
        }
        return Task.CompletedTask;
    }
}
```

### FfprobeServiceFake

```csharp
public class FfprobeServiceFake : IFfprobeService
{
    public AudioMetadata? MetadataToReturn { get; set; }
    public FfprobeException? ExceptionToThrow { get; set; }
    public List<string> ProcessedFiles { get; } = new();

    public Task<AudioMetadata> ExtractMetadataAsync(string filePath, CancellationToken ct)
    {
        ProcessedFiles.Add(filePath);

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        return Task.FromResult(MetadataToReturn ?? CreateDefaultMetadata());
    }

    private static AudioMetadata CreateDefaultMetadata() => new()
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
}
```

### TempFileManagerFake

```csharp
public class TempFileManagerFake : ITempFileManager
{
    public HashSet<string> CreatedDirectories { get; } = new();
    public HashSet<string> CleanedDirectories { get; } = new();
    public bool HasSufficientSpace { get; set; } = true;
    public long CurrentDiskUsage { get; set; } = 0;
    public long AvailableDiskSpace { get; set; } = 10L * 1024 * 1024 * 1024; // 10 GB

    public string BaseTempDirectory => "/tmp/novatune-processing-test";

    public string CreateTempDirectory(string trackId)
    {
        CreatedDirectories.Add(trackId);
        var path = Path.Combine(BaseTempDirectory, trackId);
        Directory.CreateDirectory(path);
        return path;
    }

    public void CleanupTempDirectory(string trackId)
    {
        CleanedDirectories.Add(trackId);
    }

    public string GetTempFilePath(string trackId, string fileName)
    {
        return Path.Combine(BaseTempDirectory, trackId, fileName);
    }

    public bool HasSufficientDiskSpace() => HasSufficientSpace;
    public long GetCurrentDiskUsageBytes() => CurrentDiskUsage;
    public long GetAvailableDiskSpaceBytes() => AvailableDiskSpace;
    public void CleanupOrphanedDirectories() { }
}
```

## Test Data Fixtures

```csharp
public static class FfprobeOutputFixtures
{
    public const string ValidMp3Output = """
        {
          "format": {
            "duration": "180.500000",
            "bit_rate": "320000",
            "size": "5000000"
          },
          "streams": [{
            "codec_type": "audio",
            "codec_name": "mp3",
            "codec_long_name": "MP3 (MPEG audio layer 3)",
            "sample_rate": "44100",
            "channels": 2,
            "bit_rate": "320000"
          }]
        }
        """;

    public const string ValidFlacOutput = """
        {
          "format": {
            "duration": "240.000000",
            "bit_rate": "1411200",
            "size": "42336000"
          },
          "streams": [{
            "codec_type": "audio",
            "codec_name": "flac",
            "codec_long_name": "FLAC (Free Lossless Audio Codec)",
            "sample_rate": "48000",
            "channels": 2,
            "bits_per_raw_sample": "24"
          }]
        }
        """;

    public const string MalformedJson = "{ invalid json }";

    public const string EmptyStreams = """
        {
          "format": { "duration": "180.0" },
          "streams": []
        }
        """;

    public const string MissingDuration = """
        {
          "format": { "bit_rate": "320000" },
          "streams": [{ "codec_name": "mp3", "sample_rate": "44100", "channels": 2 }]
        }
        """;

    public const string VideoStream = """
        {
          "format": { "duration": "180.0" },
          "streams": [{ "codec_type": "video", "codec_name": "h264" }]
        }
        """;
}

public static class AudioUploadedEventFixtures
{
    public static AudioUploadedEvent CreateValid(string? trackId = null, string? userId = null) => new()
    {
        TrackId = trackId ?? Ulid.NewUlid().ToString(),
        UserId = userId ?? Ulid.NewUlid().ToString(),
        ObjectKey = $"audio/{userId ?? "user"}/{trackId ?? "track"}/abc123",
        CorrelationId = Guid.NewGuid().ToString(),
        UploadedAt = DateTimeOffset.UtcNow
    };

    public static Track CreateProcessingTrack(string trackId, string userId) => new()
    {
        Id = $"Tracks/{trackId}",
        TrackId = trackId,
        UserId = userId,
        Title = "Test Track",
        ObjectKey = $"audio/{userId}/{trackId}/abc123",
        Status = TrackStatus.Processing,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
```

## Example Test Implementation

```csharp
public class AudioProcessorServiceTests : BaseTest
{
    private readonly StorageServiceFake _storageServiceFake;
    private readonly DocumentStoreFake _documentStoreFake;
    private readonly FfprobeServiceFake _ffprobeServiceFake;
    private readonly WaveformServiceFake _waveformServiceFake;
    private readonly TempFileManagerFake _tempFileManagerFake;
    private readonly AudioProcessorService _service;

    public AudioProcessorServiceTests()
    {
        _storageServiceFake = ServiceProvider.GetRequiredService<StorageServiceFake>();
        _documentStoreFake = ServiceProvider.GetRequiredService<DocumentStoreFake>();
        _ffprobeServiceFake = ServiceProvider.GetRequiredService<FfprobeServiceFake>();
        _waveformServiceFake = ServiceProvider.GetRequiredService<WaveformServiceFake>();
        _tempFileManagerFake = ServiceProvider.GetRequiredService<TempFileManagerFake>();
        _service = ServiceProvider.GetRequiredService<AudioProcessorService>();
    }

    // ============================================================================
    // Happy Path Tests
    // ============================================================================

    [Fact]
    public async Task Should_process_valid_audio_and_update_track_to_ready()
    {
        var trackId = Ulid.NewUlid().ToString();
        var userId = Ulid.NewUlid().ToString();
        var track = AudioUploadedEventFixtures.CreateProcessingTrack(trackId, userId);
        var @event = AudioUploadedEventFixtures.CreateValid(trackId, userId);

        _documentStoreFake.Tracks[$"Tracks/{trackId}"] = track;
        _storageServiceFake.Objects[@event.ObjectKey] = new byte[1000];

        var result = await _service.ProcessAsync(@event, CancellationToken.None);

        result.ShouldBeTrue();
        track.Status.ShouldBe(TrackStatus.Ready);
        track.Metadata.ShouldNotBeNull();
        track.WaveformObjectKey.ShouldStartWith($"waveforms/{userId}/{trackId}/");
    }

    // ============================================================================
    // Idempotency Tests
    // ============================================================================

    [Theory]
    [InlineData(TrackStatus.Ready)]
    [InlineData(TrackStatus.Failed)]
    [InlineData(TrackStatus.Deleted)]
    public async Task Should_skip_processing_for_non_processing_status(TrackStatus status)
    {
        var trackId = Ulid.NewUlid().ToString();
        var track = AudioUploadedEventFixtures.CreateProcessingTrack(trackId, "user");
        track.Status = status;
        var @event = AudioUploadedEventFixtures.CreateValid(trackId);

        _documentStoreFake.Tracks[$"Tracks/{trackId}"] = track;

        var result = await _service.ProcessAsync(@event, CancellationToken.None);

        result.ShouldBeTrue(); // Ack the message
        _ffprobeServiceFake.ProcessedFiles.ShouldBeEmpty(); // No processing occurred
    }

    // ============================================================================
    // Disk Space Tests
    // ============================================================================

    [Fact]
    public async Task Should_fail_fast_when_disk_space_exceeded()
    {
        var @event = AudioUploadedEventFixtures.CreateValid();
        _tempFileManagerFake.HasSufficientSpace = false;

        var result = await _service.ProcessAsync(@event, CancellationToken.None);

        result.ShouldBeFalse(); // Signal retry
        _storageServiceFake.DownloadedKeys.ShouldBeEmpty(); // No download attempted
    }

    // ============================================================================
    // Cleanup Tests
    // ============================================================================

    [Fact]
    public async Task Should_cleanup_temp_files_after_processing()
    {
        var trackId = Ulid.NewUlid().ToString();
        var track = AudioUploadedEventFixtures.CreateProcessingTrack(trackId, "user");
        var @event = AudioUploadedEventFixtures.CreateValid(trackId);

        _documentStoreFake.Tracks[$"Tracks/{trackId}"] = track;
        _storageServiceFake.Objects[@event.ObjectKey] = new byte[1000];

        await _service.ProcessAsync(@event, CancellationToken.None);

        _tempFileManagerFake.CreatedDirectories.ShouldContain(trackId);
        _tempFileManagerFake.CleanedDirectories.ShouldContain(trackId);
    }
}
```

## Coverage Targets

| Component | Target | Rationale |
|-----------|--------|-----------|
| AudioProcessorService | 90% | Core processing logic, critical path |
| FfprobeService | 85% | External process invocation, parsing |
| WaveformService | 85% | External process invocation |
| TempFileManager | 90% | File system operations, cleanup |
| DlqHandler | 90% | Error path reliability |
| MetadataValidator | 100% | Pure validation logic |
| AudioUploadedHandler | 85% | Orchestration and retry logic |

## Test Execution

```bash
# Run all unit tests
dotnet test src/unit_tests/NovaTune.UnitTests.csproj

# Run specific test class
dotnet test --filter "FullyQualifiedName~AudioProcessorServiceTests"

# Run specific test
dotnet test --filter "FullyQualifiedName~Should_process_valid_audio_and_update_track"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```
