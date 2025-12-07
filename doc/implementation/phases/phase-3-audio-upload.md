# Phase 3: Audio Upload Pipeline (FR 2.x, FR 3.x)

> **Status:** ⏳ Pending
> **Dependencies:** Phase 1 (Infrastructure), Phase 2 (User Management)
> **Milestone:** M2 - Upload

## Objective

Build a robust audio upload system with streaming to MinIO, format validation via FFprobe, and event publishing to Kafka.

---

## FR Coverage

| FR ID | Requirement | Priority | Verification |
|-------|-------------|----------|--------------|
| FR 2.1 | Supported Formats | P1 | Test |
| FR 2.2 | Validation | P1 | Test |
| FR 2.3 | Storage Pipeline | P1 | Test |
| FR 2.4 | Metadata Capture | P1 | Test |
| FR 2.5 | Feedback | P2 | Test |
| FR 2.6 | Background Tasks | P1 | Test |
| FR 3.1 | Duration Extraction | P1 | Test |
| FR 3.2 | Track IDs | P1 | Test |
| FR 3.3 | Optional Waveform Generation | P3 | E2E |

## NFR Coverage

| NFR ID | Requirement | Implementation |
|--------|-------------|----------------|
| NF-1.1 | Throughput | 50 concurrent uploads per node |
| NF-1.2 | Latency | <3s p50 for files ≤50 MB |
| NF-1.6 | Audio Processing Limits | 30s FFprobe timeout |
| NF-2.2 | Resilience | Polly retry policies |
| NF-3.5 | Input Validation | MIME type, size, format checks |
| NF-6.3 | Event Stream Governance | `audio-uploaded` schema |
| NF-9.2 | Event Schema Evolution | Versioned JSON payloads |

---

## Tasks

### Task 3.1: Storage Service Interface & Implementation

**Priority:** P1 (Must-have)

Create MinIO integration with streaming upload support.

#### Subtasks

- [ ] **3.1.1** Create `IStorageService` interface:
  ```csharp
  public interface IStorageService
  {
      Task<UploadResult> UploadAsync(
          Stream stream,
          string objectKey,
          string contentType,
          long contentLength,
          string? contentMd5 = null,
          CancellationToken cancellationToken = default);

      Task<Stream> DownloadAsync(
          string objectKey,
          CancellationToken cancellationToken = default);

      Task<bool> ExistsAsync(
          string objectKey,
          CancellationToken cancellationToken = default);

      Task DeleteAsync(
          string objectKey,
          CancellationToken cancellationToken = default);

      Task<string> GeneratePresignedUrlAsync(
          string objectKey,
          TimeSpan expiry,
          CancellationToken cancellationToken = default);
  }

  public record UploadResult(
      string ObjectKey,
      string ETag,
      string Checksum,
      long SizeBytes);
  ```

- [ ] **3.1.2** Implement `MinioStorageService`:
  ```csharp
  public sealed class MinioStorageService : IStorageService
  {
      private readonly IMinioClient _client;
      private readonly MinioOptions _options;
      private const int MultipartThreshold = 5 * 1024 * 1024; // 5MB

      public async Task<UploadResult> UploadAsync(
          Stream stream,
          string objectKey,
          string contentType,
          long contentLength,
          string? contentMd5 = null,
          CancellationToken ct = default)
      {
          // Compute SHA-256 while streaming
          using var sha256 = SHA256.Create();
          using var hashingStream = new CryptoStream(
              stream, sha256, CryptoStreamMode.Read);

          // Validate Content-MD5 if provided
          if (contentMd5 != null)
          {
              var actualMd5 = await ComputeMd5Async(stream, ct);
              if (actualMd5 != contentMd5)
                  throw new ChecksumMismatchException(contentMd5, actualMd5);
              stream.Position = 0;
          }

          // Upload with multipart if large
          if (contentLength > MultipartThreshold)
              return await MultipartUploadAsync(hashingStream, objectKey, contentType, ct);

          var args = new PutObjectArgs()
              .WithBucket(_options.Bucket)
              .WithObject(objectKey)
              .WithStreamData(hashingStream)
              .WithObjectSize(contentLength)
              .WithContentType(contentType);

          var response = await _client.PutObjectAsync(args, ct);

          return new UploadResult(
              objectKey,
              response.Etag,
              Convert.ToHexString(sha256.Hash!),
              contentLength);
      }
  }
  ```

- [ ] **3.1.3** Implement multipart upload for files >5MB:
  ```csharp
  private async Task<UploadResult> MultipartUploadAsync(
      Stream stream,
      string objectKey,
      string contentType,
      CancellationToken ct)
  {
      const int partSize = 10 * 1024 * 1024; // 10MB parts
      // Implementation using MinIO multipart API
  }
  ```

- [ ] **3.1.4** Configure object key format:
  ```
  {environment}/{userId}/{trackId}/{version}/{sanitized_filename}
  Example: dev/users-123/tracks-456/v1/my-song.mp3
  ```

- [ ] **3.1.5** Add Polly resilience policies:
  ```csharp
  services.AddHttpClient<IMinioClient, MinioClient>()
      .AddPolicyHandler(Policy
          .Handle<MinioException>()
          .WaitAndRetryAsync(5, retryAttempt =>
              TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
      .AddPolicyHandler(Policy
          .Handle<MinioException>()
          .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
  ```

- [ ] **3.1.6** Write integration tests with Testcontainers

#### Acceptance Criteria
- Streaming upload works without buffering entire file
- Multipart upload handles large files
- SHA-256 checksum computed on every upload
- Content-MD5 validation returns 400 on mismatch
- Retry policy handles transient failures

---

### Task 3.2: Upload Validation Pipeline

**Priority:** P1 (Must-have)

Implement comprehensive file validation before storage.

#### Subtasks

- [ ] **3.2.1** Create `IUploadValidator` interface:
  ```csharp
  public interface IUploadValidator
  {
      Task<ValidationResult> ValidateAsync(
          IFormFile file,
          CancellationToken ct = default);
  }

  public record ValidationResult(
      bool IsValid,
      string? ErrorCode,
      string? ErrorMessage);
  ```

- [ ] **3.2.2** Implement MIME type validation:
  ```csharp
  private static readonly HashSet<string> AllowedMimeTypes = new()
  {
      "audio/mpeg",       // MP3
      "audio/wav",        // WAV
      "audio/x-wav",      // WAV (alternative)
      "audio/flac",       // FLAC
      "audio/x-flac",     // FLAC (alternative)
      "audio/aac",        // AAC
      "audio/ogg",        // OGG
      "audio/x-m4a",      // M4A
      "audio/mp4"         // M4A (alternative)
  };
  ```

- [ ] **3.2.3** Implement file size validation:
  ```csharp
  public class FileSizeValidator
  {
      private readonly long _maxSizeBytes;

      public FileSizeValidator(IOptions<UploadOptions> options)
      {
          _maxSizeBytes = options.Value.MaxFileSizeMb * 1024 * 1024;
      }

      public ValidationResult Validate(long fileSize)
      {
          if (fileSize > _maxSizeBytes)
              return ValidationResult.Fail(
                  "FILE_TOO_LARGE",
                  $"File exceeds maximum size of {_maxSizeBytes / 1024 / 1024}MB");
          return ValidationResult.Success();
      }
  }
  ```

- [ ] **3.2.4** Implement filename sanitization:
  ```csharp
  public static class FileNameSanitizer
  {
      private static readonly Regex InvalidChars =
          new(@"[<>:""/\\|?*\x00-\x1F]", RegexOptions.Compiled);

      private static readonly string[] PathTraversalPatterns =
          { "..", "./", "../", ".\\", "..\\", "~/" };

      public static string Sanitize(string filename)
      {
          // Check for path traversal attempts
          foreach (var pattern in PathTraversalPatterns)
          {
              if (filename.Contains(pattern))
                  throw new SecurityException("Path traversal detected");
          }

          // Remove invalid characters
          var sanitized = InvalidChars.Replace(filename, "_");

          // Limit length
          if (sanitized.Length > 255)
              sanitized = sanitized[..255];

          return sanitized;
      }
  }
  ```

- [ ] **3.2.5** Implement FFprobe format verification:
  ```csharp
  public class FFprobeValidator
  {
      private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

      public async Task<AudioInfo?> AnalyzeAsync(
          Stream stream,
          CancellationToken ct)
      {
          using var cts = CancellationTokenSource
              .CreateLinkedTokenSource(ct);
          cts.CancelAfter(_timeout);

          // Save to temp file for FFprobe
          var tempPath = Path.GetTempFileName();
          try
          {
              await using (var file = File.Create(tempPath))
                  await stream.CopyToAsync(file, cts.Token);

              var result = await RunFFprobeAsync(tempPath, cts.Token);
              return ParseFFprobeOutput(result);
          }
          finally
          {
              File.Delete(tempPath);
          }
      }
  }
  ```

- [ ] **3.2.6** Add duration limit validation (60 minutes max)

- [ ] **3.2.7** Write property-based tests with FsCheck:
  ```csharp
  [Property]
  public Property SanitizedFilenameNeverContainsPathTraversal(
      NonEmptyString filename)
  {
      var sanitized = FileNameSanitizer.Sanitize(filename.Get);
      return (!sanitized.Contains("..") &&
              !sanitized.Contains("./") &&
              !sanitized.Contains("~")).ToProperty();
  }
  ```

#### Acceptance Criteria
- Invalid MIME types rejected with 415
- Oversized files rejected with 413
- Path traversal attempts rejected with 400
- FFprobe timeout at 30 seconds
- Property-based tests cover edge cases

---

### Task 3.3: Metadata Extraction Service

**Priority:** P1 (Must-have)

Extract audio metadata using FFprobe.

#### Subtasks

- [ ] **3.3.1** Create `IMetadataExtractor` interface:
  ```csharp
  public interface IMetadataExtractor
  {
      Task<AudioMetadata> ExtractAsync(
          string filePath,
          CancellationToken ct = default);
  }

  public record AudioMetadata(
      string Format,
      int Bitrate,
      int SampleRate,
      int Channels,
      long FileSizeBytes,
      TimeSpan Duration,
      string? MimeType,
      ID3Tags? Tags);

  public record ID3Tags(
      string? Title,
      string? Artist,
      string? Album,
      int? Year,
      string? Genre);
  ```

- [ ] **3.3.2** Implement `FFprobeMetadataExtractor`:
  ```csharp
  public sealed class FFprobeMetadataExtractor : IMetadataExtractor
  {
      public async Task<AudioMetadata> ExtractAsync(
          string filePath,
          CancellationToken ct)
      {
          var args = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";
          var result = await RunProcessAsync("ffprobe", args, ct);

          var json = JsonDocument.Parse(result.StandardOutput);
          var format = json.RootElement.GetProperty("format");
          var stream = json.RootElement
              .GetProperty("streams")
              .EnumerateArray()
              .First(s => s.GetProperty("codec_type").GetString() == "audio");

          return new AudioMetadata(
              Format: format.GetProperty("format_name").GetString()!,
              Bitrate: int.Parse(format.GetProperty("bit_rate").GetString()!),
              SampleRate: stream.GetProperty("sample_rate").GetInt32(),
              Channels: stream.GetProperty("channels").GetInt32(),
              FileSizeBytes: long.Parse(format.GetProperty("size").GetString()!),
              Duration: TimeSpan.FromSeconds(
                  double.Parse(format.GetProperty("duration").GetString()!)),
              MimeType: GetMimeType(format.GetProperty("format_name").GetString()!),
              Tags: ExtractId3Tags(format));
      }
  }
  ```

- [ ] **3.3.3** Map format names to MIME types

- [ ] **3.3.4** Extract ID3 tags when available

- [ ] **3.3.5** Write tests for all supported formats:
  - MP3 (various bitrates)
  - WAV (16-bit, 24-bit)
  - FLAC (lossless)
  - AAC/M4A
  - OGG Vorbis

#### Acceptance Criteria
- All supported formats extract correctly
- Duration accurate to milliseconds
- ID3 tags extracted when present
- Handles corrupted files gracefully

---

### Task 3.4: Upload API Endpoint

**Priority:** P1 (Must-have)

Create the main upload endpoint with streaming support.

#### Subtasks

- [ ] **3.4.1** Create `POST /api/v1/tracks/upload`:
  ```csharp
  app.MapPost("/api/v1/tracks/upload", async (
      HttpRequest request,
      ClaimsPrincipal user,
      IUploadService uploadService,
      CancellationToken ct) =>
  {
      if (!request.HasFormContentType)
          return Results.BadRequest("Multipart form data required");

      var form = await request.ReadFormAsync(ct);
      var file = form.Files.GetFile("file");

      if (file is null)
          return Results.BadRequest("No file provided");

      var userId = user.GetUserId();
      var correlationId = Guid.NewGuid().ToString();

      var result = await uploadService.UploadAsync(
          userId,
          file,
          correlationId,
          ct);

      return result.Match(
          success => Results.Created(
              $"/api/v1/tracks/{success.TrackId}",
              new UploadResponse(
                  success.TrackId,
                  success.CorrelationId,
                  "Processing")),
          error => error switch
          {
              ValidationError ve => Results.BadRequest(ve),
              FileTooLargeError => Results.StatusCode(413),
              UnsupportedFormatError => Results.StatusCode(415),
              _ => Results.Problem()
          });
  })
  .RequireAuthorization()
  .DisableAntiforgery()
  .WithName("UploadTrack")
  .WithOpenApi();
  ```

- [ ] **3.4.2** Implement `IUploadService`:
  ```csharp
  public interface IUploadService
  {
      Task<Result<UploadSuccess, UploadError>> UploadAsync(
          string userId,
          IFormFile file,
          string correlationId,
          CancellationToken ct);
  }

  public sealed class UploadService : IUploadService
  {
      public async Task<Result<UploadSuccess, UploadError>> UploadAsync(...)
      {
          // 1. Validate file (size, type, name)
          var validation = await _validator.ValidateAsync(file, ct);
          if (!validation.IsValid)
              return new ValidationError(validation.ErrorCode, validation.ErrorMessage);

          // 2. Create track document
          var track = new Track
          {
              Id = $"Tracks/{Guid.NewGuid()}",
              UserId = userId,
              Title = Path.GetFileNameWithoutExtension(file.FileName),
              Status = TrackStatus.Processing,
              CreatedAt = DateTimeOffset.UtcNow
          };

          // 3. Generate object key
          var objectKey = GenerateObjectKey(userId, track.Id, file.FileName);

          // 4. Stream to MinIO
          await using var stream = file.OpenReadStream();
          var uploadResult = await _storage.UploadAsync(
              stream,
              objectKey,
              file.ContentType,
              file.Length,
              Request.Headers["Content-MD5"],
              ct);

          // 5. Update track with storage info
          track.ObjectKey = uploadResult.ObjectKey;
          track.Checksum = uploadResult.Checksum;

          // 6. Save track document
          await _trackRepository.CreateAsync(track, ct);

          // 7. Queue metadata extraction
          await _messageQueue.PublishAsync(
              new ExtractMetadataCommand(track.Id, objectKey, correlationId),
              ct);

          // 8. Publish upload event
          await _eventPublisher.PublishAsync(
              new AudioUploadedEvent(track.Id, userId, objectKey, correlationId),
              ct);

          return new UploadSuccess(track.Id, correlationId);
      }
  }
  ```

- [ ] **3.4.3** Configure request size limits:
  ```csharp
  builder.WebHost.ConfigureKestrel(options =>
  {
      options.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // 200MB
  });
  ```

- [ ] **3.4.4** Add request timeout handling

- [ ] **3.4.5** Write integration tests for complete upload flow

#### Acceptance Criteria
- Streaming upload without buffering
- Proper status codes for all error cases
- Track created in RavenDB
- Kafka event published
- Metadata extraction queued

---

### Task 3.5: Progress Tracking (SSE)

**Priority:** P2 (Should-have)

Implement Server-Sent Events for upload progress.

#### Subtasks

- [ ] **3.5.1** Create `GET /api/v1/tracks/upload/progress/{correlationId}`:
  ```csharp
  app.MapGet("/api/v1/tracks/upload/progress/{correlationId}",
      async (string correlationId, HttpContext context, CancellationToken ct) =>
  {
      context.Response.Headers.Append("Content-Type", "text/event-stream");
      context.Response.Headers.Append("Cache-Control", "no-cache");
      context.Response.Headers.Append("Connection", "keep-alive");

      var channel = _progressTracker.GetChannel(correlationId);

      await foreach (var progress in channel.ReadAllAsync(ct))
      {
          var json = JsonSerializer.Serialize(progress);
          await context.Response.WriteAsync($"data: {json}\n\n", ct);
          await context.Response.Body.FlushAsync(ct);
      }
  }).RequireAuthorization();
  ```

- [ ] **3.5.2** Create `IProgressTracker`:
  ```csharp
  public interface IProgressTracker
  {
      void ReportProgress(string correlationId, UploadProgress progress);
      ChannelReader<UploadProgress> GetChannel(string correlationId);
  }

  public record UploadProgress(
      string Stage,        // "uploading", "validating", "extracting", "complete", "failed"
      int PercentComplete,
      string? Message,
      string? ErrorCode);
  ```

- [ ] **3.5.3** Report progress from upload pipeline stages

- [ ] **3.5.4** Add timeout for stale connections

- [ ] **3.5.5** Write E2E tests for SSE stream

#### Acceptance Criteria
- Progress events stream in real-time
- Connection closes on completion/failure
- Stale connections cleaned up

---

### Task 3.6: Kafka Event Publishing

**Priority:** P1 (Must-have)

Publish upload events to Kafka for downstream processing.

#### Subtasks

- [ ] **3.6.1** Create event schema:
  ```csharp
  public record AudioUploadedEvent(
      [property: JsonPropertyName("schemaVersion")]
      int SchemaVersion,

      [property: JsonPropertyName("eventType")]
      string EventType,

      [property: JsonPropertyName("trackId")]
      string TrackId,

      [property: JsonPropertyName("userId")]
      string UserId,

      [property: JsonPropertyName("objectKey")]
      string ObjectKey,

      [property: JsonPropertyName("mimeType")]
      string MimeType,

      [property: JsonPropertyName("fileSizeBytes")]
      long FileSizeBytes,

      [property: JsonPropertyName("checksum")]
      string Checksum,

      [property: JsonPropertyName("correlationId")]
      string CorrelationId,

      [property: JsonPropertyName("timestamp")]
      DateTimeOffset Timestamp
  ) {
      public AudioUploadedEvent(
          string trackId, string userId, string objectKey,
          string mimeType, long fileSize, string checksum,
          string correlationId)
          : this(1, "audio-uploaded", trackId, userId, objectKey,
                 mimeType, fileSize, checksum, correlationId,
                 DateTimeOffset.UtcNow) { }
  }
  ```

- [ ] **3.6.2** Create `IEventPublisher`:
  ```csharp
  public interface IEventPublisher
  {
      Task PublishAsync<T>(T @event, CancellationToken ct = default)
          where T : class;
  }
  ```

- [ ] **3.6.3** Implement `KafkaEventPublisher`:
  ```csharp
  public sealed class KafkaEventPublisher : IEventPublisher
  {
      private readonly IProducer<string, string> _producer;

      public async Task PublishAsync<T>(T @event, CancellationToken ct)
      {
          var message = new Message<string, string>
          {
              Key = GetEventKey(@event),
              Value = JsonSerializer.Serialize(@event)
          };

          await _producer.ProduceAsync("audio-events", message, ct);
      }
  }
  ```

- [ ] **3.6.4** Configure Kafka topic:
  - Topic: `audio-events`
  - Retention: 30 days
  - Partitions: 6 (scale with upload volume)

- [ ] **3.6.5** Add dead-letter queue fallback to RabbitMQ

- [ ] **3.6.6** Write integration tests for event publishing

#### Acceptance Criteria
- Events published within 5 seconds of upload
- Schema versioning for evolution
- Dead-letter handling on Kafka failure

---

### Task 3.7: Background Metadata Processing

**Priority:** P1 (Must-have)

Process metadata extraction asynchronously via RabbitMQ.

#### Subtasks

- [ ] **3.7.1** Create `ExtractMetadataCommand`:
  ```csharp
  public record ExtractMetadataCommand(
      string TrackId,
      string ObjectKey,
      string CorrelationId);
  ```

- [ ] **3.7.2** Implement metadata extraction worker:
  ```csharp
  public class MetadataExtractionWorker : BackgroundService
  {
      protected override async Task ExecuteAsync(CancellationToken ct)
      {
          await foreach (var command in _channel.ReadAllAsync(ct))
          {
              try
              {
                  await ProcessCommandAsync(command, ct);
              }
              catch (Exception ex)
              {
                  _logger.LogError(ex, "Metadata extraction failed for {TrackId}",
                      command.TrackId);
                  await _deadLetter.PublishAsync(command, ex);
              }
          }
      }

      private async Task ProcessCommandAsync(
          ExtractMetadataCommand command,
          CancellationToken ct)
      {
          // 1. Download file from MinIO
          var stream = await _storage.DownloadAsync(command.ObjectKey, ct);

          // 2. Save to temp file
          var tempPath = Path.GetTempFileName();
          await using (var file = File.Create(tempPath))
              await stream.CopyToAsync(file, ct);

          try
          {
              // 3. Extract metadata
              var metadata = await _extractor.ExtractAsync(tempPath, ct);

              // 4. Update track document
              await _trackRepository.UpdateMetadataAsync(
                  command.TrackId, metadata, ct);

              // 5. Mark track as ready
              await _trackRepository.SetStatusAsync(
                  command.TrackId, TrackStatus.Ready, ct);

              // 6. Report progress complete
              _progressTracker.ReportProgress(command.CorrelationId,
                  new UploadProgress("complete", 100, "Processing complete", null));
          }
          finally
          {
              File.Delete(tempPath);
          }
      }
  }
  ```

- [ ] **3.7.3** Configure RabbitMQ queue:
  - Queue: `metadata-extraction`
  - Prefetch: 5 messages
  - Retry with exponential backoff

- [ ] **3.7.4** Add dead-letter queue for failed extractions

- [ ] **3.7.5** Add metrics for processing time and failure rates

- [ ] **3.7.6** Write integration tests with Testcontainers

#### Acceptance Criteria
- Metadata extracted within 30 seconds
- Failed extractions go to DLQ
- Track status updated correctly
- Metrics published

---

### Task 3.8: Optional Waveform Generation

**Priority:** P3 (Nice-to-have)

Generate waveform data for visualization.

#### Subtasks

- [ ] **3.8.1** Create waveform command:
  ```csharp
  public record GenerateWaveformCommand(
      string TrackId,
      string ObjectKey);
  ```

- [ ] **3.8.2** Implement waveform generation:
  ```csharp
  public async Task<float[]> GenerateWaveformAsync(
      string filePath,
      int samples = 1000,
      CancellationToken ct = default)
  {
      // Use FFmpeg to extract audio samples
      var args = $"-i \"{filePath}\" -ac 1 -filter:a volumedetect -f null -";
      // Parse and downsample to target resolution
  }
  ```

- [ ] **3.8.3** Configure RabbitMQ queue: `waveform-jobs`

- [ ] **3.8.4** Store waveform data in track document or MinIO

- [ ] **3.8.5** Add feature flag for enabling/disabling

#### Acceptance Criteria
- 1000-sample waveform array generated
- Processing time <60 seconds
- Feature toggleable via configuration

---

### Task 3.9: RavenDB Track Documents

**Priority:** P1 (Must-have)

Configure RavenDB for track storage.

#### Subtasks

- [ ] **3.9.1** Create RavenDB indexes:
  ```csharp
  public class Tracks_ByUserId : AbstractIndexCreationTask<Track>
  {
      public Tracks_ByUserId()
      {
          Map = tracks => from track in tracks
                          select new
                          {
                              track.UserId,
                              track.Status,
                              track.CreatedAt
                          };
      }
  }

  public class Tracks_ByUploadDate : AbstractIndexCreationTask<Track>
  {
      public Tracks_ByUploadDate()
      {
          Map = tracks => from track in tracks
                          select new
                          {
                              track.UserId,
                              track.CreatedAt,
                              track.Status
                          };

          Sort(x => x.CreatedAt, SortOptions.Descending);
      }
  }
  ```

- [ ] **3.9.2** Implement `ITrackRepository`:
  ```csharp
  public interface ITrackRepository
  {
      Task<Track?> GetByIdAsync(string id, CancellationToken ct);
      Task<Track> CreateAsync(Track track, CancellationToken ct);
      Task UpdateMetadataAsync(string id, AudioMetadata metadata, CancellationToken ct);
      Task SetStatusAsync(string id, TrackStatus status, CancellationToken ct);
      Task<IReadOnlyList<Track>> GetByUserIdAsync(string userId, int skip, int take, CancellationToken ct);
  }
  ```

- [ ] **3.9.3** Add optimistic concurrency

- [ ] **3.9.4** Deploy indexes on startup

- [ ] **3.9.5** Write integration tests

#### Acceptance Criteria
- Indexes created and used
- CRUD operations work correctly
- Optimistic concurrency prevents conflicts

---

## Infrastructure Setup

- [ ] MinIO buckets: `novatune-dev-audio`, `novatune-test-audio`
- [ ] MinIO bucket policies (private, SSE-S3)
- [ ] MinIO lifecycle rules (3 versions, 7 day expiry)
- [ ] Kafka topic: `audio-events` (30-day retention)
- [ ] RabbitMQ queues: `metadata-extraction`, `waveform-jobs` (optional)
- [ ] FFmpeg/FFprobe in Docker image

---

## Testing Requirements

| Type | Target | Coverage |
|------|--------|----------|
| Unit | Validation logic | 100% |
| Unit | Metadata extraction | All supported formats |
| Unit | Checksum computation | SHA-256, MD5 validation |
| Property-based | Filename sanitization | FsCheck edge cases |
| Integration | MinIO upload | Success, failure, multipart |
| Integration | Kafka publishing | Event schema validation |
| Integration | End-to-end upload | Full pipeline |
| Integration | Checksum mismatch | 400 response verification |
| Load | Concurrent uploads | 50 simultaneous |

---

## Exit Criteria

- [ ] Upload accepts all whitelisted formats
- [ ] Rejects invalid formats with 415 status
- [ ] Rejects oversized files with 413 status
- [ ] Progress events stream via SSE
- [ ] Metadata captured accurately in RavenDB
- [ ] SHA-256 checksum computed and stored for each upload
- [ ] Content-MD5 mismatch returns 400 Bad Request
- [ ] Kafka event published within 5 seconds
- [ ] Upload latency <3s p50 for ≤50 MB files
- [ ] ≥80% test coverage for upload pipeline

---

## Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| FFprobe timeout on large files | Medium | 30s limit, background processing |
| MinIO connection failures | High | Polly retry with circuit breaker |
| Kafka unavailability | High | RabbitMQ dead-letter fallback |
| Memory pressure from large uploads | High | Streaming (no buffering) |

---

## Navigation

← [Phase 2: User Management](phase-2-user-management.md) | [Overview](../overview.md) | [Phase 4: Storage & Access Control →](phase-4-storage-access.md)
