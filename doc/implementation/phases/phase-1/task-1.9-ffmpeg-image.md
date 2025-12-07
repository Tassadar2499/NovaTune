# Task 1.9: FFmpeg Base Image

> **Phase:** 1 - Infrastructure & Domain Foundation
> **Priority:** P2 (Should-have)
> **Status:** Pending

## Description

Configure Docker image with FFmpeg/FFprobe for audio processing.

---

## Subtasks

### 1.9.1 Create Dockerfile with FFmpeg

- [ ] Create `src/NovaTuneApp/NovaTuneApp.ApiService/Dockerfile`:

```dockerfile
# ===========================================
# Build Stage
# ===========================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["NovaTuneApp.sln", "./"]
COPY ["NovaTuneApp.ApiService/NovaTuneApp.ApiService.csproj", "NovaTuneApp.ApiService/"]
COPY ["NovaTuneApp.ServiceDefaults/NovaTuneApp.ServiceDefaults.csproj", "NovaTuneApp.ServiceDefaults/"]

# Restore dependencies
RUN dotnet restore "NovaTuneApp.ApiService/NovaTuneApp.ApiService.csproj"

# Copy source code
COPY . .

# Build
WORKDIR "/src/NovaTuneApp.ApiService"
RUN dotnet build "NovaTuneApp.ApiService.csproj" -c Release -o /app/build

# ===========================================
# Publish Stage
# ===========================================
FROM build AS publish
RUN dotnet publish "NovaTuneApp.ApiService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ===========================================
# Runtime Stage
# ===========================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base

# Install FFmpeg and FFprobe
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ffmpeg \
        && rm -rf /var/lib/apt/lists/*

# Verify FFmpeg installation
RUN ffmpeg -version && ffprobe -version

# Create non-root user for security
RUN groupadd -r novatune && useradd -r -g novatune novatune

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# ===========================================
# Final Stage
# ===========================================
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create temp directory for audio processing
RUN mkdir -p /app/temp && chown -R novatune:novatune /app

# Switch to non-root user
USER novatune

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "NovaTuneApp.ApiService.dll"]
```

---

### 1.9.2 Verify FFmpeg and FFprobe

- [ ] Create verification script and service

**FFmpeg wrapper service:**
```csharp
// Infrastructure/FFmpeg/FFmpegService.cs
namespace NovaTuneApp.ApiService.Infrastructure.FFmpeg;

public interface IFFmpegService
{
    Task<FFprobeResult> ProbeAsync(string filePath, CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    string GetVersion();
}

public class FFmpegService : IFFmpegService
{
    private readonly ILogger<FFmpegService> _logger;
    private readonly string _ffprobePath;
    private readonly string _ffmpegPath;

    public FFmpegService(ILogger<FFmpegService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _ffprobePath = configuration["FFmpeg:FFprobePath"] ?? "ffprobe";
        _ffmpegPath = configuration["FFmpeg:FFmpegPath"] ?? "ffmpeg";
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null) return false;

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FFprobe is not available");
            return false;
        }
    }

    public string GetVersion()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null) return "Unknown";

            var output = process.StandardOutput.ReadLine();
            process.WaitForExit();

            return output ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    public async Task<FFprobeResult> ProbeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start FFprobe");

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            _logger.LogError("FFprobe failed: {Error}", error);
            throw new FFmpegException($"FFprobe failed with exit code {process.ExitCode}: {error}");
        }

        return JsonSerializer.Deserialize<FFprobeResult>(output)
            ?? throw new FFmpegException("Failed to parse FFprobe output");
    }
}

public class FFmpegException : Exception
{
    public FFmpegException(string message) : base(message) { }
    public FFmpegException(string message, Exception inner) : base(message, inner) { }
}
```

**FFprobe result models:**
```csharp
// Infrastructure/FFmpeg/FFprobeResult.cs
namespace NovaTuneApp.ApiService.Infrastructure.FFmpeg;

public record FFprobeResult
{
    [JsonPropertyName("streams")]
    public FFprobeStream[] Streams { get; init; } = [];

    [JsonPropertyName("format")]
    public FFprobeFormat? Format { get; init; }
}

public record FFprobeStream
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("codec_type")]
    public string CodecType { get; init; } = string.Empty;

    [JsonPropertyName("codec_name")]
    public string CodecName { get; init; } = string.Empty;

    [JsonPropertyName("sample_rate")]
    public string? SampleRate { get; init; }

    [JsonPropertyName("channels")]
    public int? Channels { get; init; }

    [JsonPropertyName("bit_rate")]
    public string? BitRate { get; init; }

    [JsonPropertyName("duration")]
    public string? Duration { get; init; }
}

public record FFprobeFormat
{
    [JsonPropertyName("filename")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("format_name")]
    public string FormatName { get; init; } = string.Empty;

    [JsonPropertyName("format_long_name")]
    public string FormatLongName { get; init; } = string.Empty;

    [JsonPropertyName("duration")]
    public string? Duration { get; init; }

    [JsonPropertyName("size")]
    public string? Size { get; init; }

    [JsonPropertyName("bit_rate")]
    public string? BitRate { get; init; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; init; }
}
```

---

### 1.9.3 Add Integration Test

- [ ] Add integration test for FFprobe execution

```csharp
// NovaTuneApp.Tests/Integration/FFmpegTests.cs
namespace NovaTuneApp.Tests.Integration;

[Trait("Category", "Integration")]
public class FFmpegTests
{
    [Fact]
    public async Task FFprobe_IsAvailable()
    {
        // Arrange
        var logger = new Mock<ILogger<FFmpegService>>();
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["FFmpeg:FFprobePath"]).Returns("ffprobe");

        var service = new FFmpegService(logger.Object, config.Object);

        // Act
        var isAvailable = await service.IsAvailableAsync();

        // Assert
        Assert.True(isAvailable, "FFprobe should be available");
    }

    [Fact]
    public void FFprobe_ReturnsVersion()
    {
        // Arrange
        var logger = new Mock<ILogger<FFmpegService>>();
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["FFmpeg:FFprobePath"]).Returns("ffprobe");

        var service = new FFmpegService(logger.Object, config.Object);

        // Act
        var version = service.GetVersion();

        // Assert
        Assert.NotEqual("Unknown", version);
        Assert.Contains("ffprobe", version, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FFprobe_CanAnalyzeAudioFile()
    {
        // Arrange
        var logger = new Mock<ILogger<FFmpegService>>();
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["FFmpeg:FFprobePath"]).Returns("ffprobe");

        var service = new FFmpegService(logger.Object, config.Object);

        // Create a test audio file (sine wave)
        var tempFile = Path.GetTempFileName() + ".wav";
        try
        {
            // Generate a 1-second test tone using ffmpeg
            var generateProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -f lavfi -i \"sine=frequency=440:duration=1\" \"{tempFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            await generateProcess!.WaitForExitAsync();

            // Act
            var result = await service.ProbeAsync(tempFile);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Format);
            Assert.Contains(result.Streams, s => s.CodecType == "audio");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
```

---

### 1.9.4 Document FFmpeg Requirements

- [ ] Document FFmpeg version requirements

**FFmpeg Version Requirements:**

| Component | Minimum Version | Recommended |
|-----------|-----------------|-------------|
| FFmpeg | 5.0 | 6.0+ |
| FFprobe | 5.0 | 6.0+ |

**Required Codecs:**
- Audio: MP3, AAC, FLAC, WAV, OGG/Vorbis, OPUS
- Container: MP4, WebM, OGG

**Required Filters:**
- `loudnorm` - Loudness normalization
- `silencedetect` - Silence detection

**Verify codecs:**
```bash
ffmpeg -codecs | grep -E "(mp3|aac|flac|vorbis|opus)"
```

**Configuration in `appsettings.json`:**
```json
{
  "FFmpeg": {
    "FFmpegPath": "ffmpeg",
    "FFprobePath": "ffprobe",
    "TempDirectory": "/app/temp",
    "MaxConcurrentProcesses": 4,
    "Timeout": "00:05:00"
  }
}
```

---

## Docker Compose Integration

**Add to `docker-compose.yml` for testing:**
```yaml
services:
  api:
    build:
      context: ./src/NovaTuneApp
      dockerfile: NovaTuneApp.ApiService/Dockerfile
    ports:
      - "5000:8080"
      - "5001:8081"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
    volumes:
      - ./temp:/app/temp
    depends_on:
      - ravendb
      - minio
```

---

## Acceptance Criteria

- [ ] Docker image includes FFmpeg
- [ ] FFprobe can analyze audio files
- [ ] Integration test passes
- [ ] Non-root user in container
- [ ] Health check configured

---

## Verification Commands

```bash
# Build Docker image
docker build -t novatune-api -f src/NovaTuneApp/NovaTuneApp.ApiService/Dockerfile src/NovaTuneApp

# Verify FFmpeg in container
docker run --rm novatune-api ffmpeg -version
docker run --rm novatune-api ffprobe -version

# Test audio analysis
docker run --rm novatune-api ffprobe -v quiet -print_format json -show_format /dev/null

# Run integration tests
dotnet test --filter "Category=Integration" --filter "FullyQualifiedName~FFmpeg"
```

---

## File Checklist

- [ ] `NovaTuneApp.ApiService/Dockerfile`
- [ ] `Infrastructure/FFmpeg/FFmpegService.cs`
- [ ] `Infrastructure/FFmpeg/FFprobeResult.cs`
- [ ] `Infrastructure/FFmpeg/FFmpegException.cs`
- [ ] `NovaTuneApp.Tests/Integration/FFmpegTests.cs`
- [ ] `appsettings.json` (FFmpeg section)

---

## Navigation

[Task 1.8: Secrets Management](task-1.8-secrets-management.md) | [Phase 1 Overview](overview.md) | [Task 1.10: CI Pipeline](task-1.10-ci-pipeline.md)
