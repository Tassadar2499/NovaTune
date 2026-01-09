using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Models;

namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Implementation of ffprobe metadata extraction.
/// Command: ffprobe -v quiet -print_format json -show_format -show_streams "{input_file}"
/// </summary>
public class FfprobeService : IFfprobeService
{
    private readonly ILogger<FfprobeService> _logger;
    private readonly AudioProcessorOptions _options;

    public FfprobeService(
        ILogger<FfprobeService> logger,
        IOptions<AudioProcessorOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<AudioMetadata> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Running ffprobe on {FilePath}", filePath);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.FfprobeTimeoutSeconds));

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError("ffprobe failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                throw new FfprobeException(
                    $"ffprobe failed with exit code {process.ExitCode}",
                    ProcessingFailureReason.CorruptedFile);
            }

            return ParseFfprobeOutput(output, filePath);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("ffprobe timed out after {Timeout}s for {FilePath}", _options.FfprobeTimeoutSeconds, filePath);
            throw new FfprobeException(
                $"ffprobe timed out after {_options.FfprobeTimeoutSeconds} seconds",
                ProcessingFailureReason.FfprobeTimeout);
        }
        catch (FfprobeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ffprobe execution failed for {FilePath}", filePath);
            throw new FfprobeException(
                "ffprobe execution failed",
                ProcessingFailureReason.CorruptedFile,
                ex);
        }
    }

    private AudioMetadata ParseFfprobeOutput(string jsonOutput, string filePath)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonOutput);
            var root = doc.RootElement;

            // Parse format section
            if (!root.TryGetProperty("format", out var format))
            {
                throw new FfprobeException("ffprobe output missing 'format' section", ProcessingFailureReason.CorruptedFile);
            }

            // Parse duration
            TimeSpan duration;
            if (format.TryGetProperty("duration", out var durationProp))
            {
                if (double.TryParse(durationProp.GetString(), out var durationSeconds))
                {
                    duration = TimeSpan.FromSeconds(durationSeconds);
                }
                else
                {
                    throw new FfprobeException("Invalid duration format", ProcessingFailureReason.InvalidDuration);
                }
            }
            else
            {
                throw new FfprobeException("ffprobe output missing duration", ProcessingFailureReason.InvalidDuration);
            }

            // Parse bit rate
            var bitRate = 0;
            if (format.TryGetProperty("bit_rate", out var bitRateProp))
            {
                int.TryParse(bitRateProp.GetString(), out bitRate);
            }

            // Parse streams section to find audio stream
            if (!root.TryGetProperty("streams", out var streams) || streams.GetArrayLength() == 0)
            {
                throw new FfprobeException("ffprobe output missing audio streams", ProcessingFailureReason.CorruptedFile);
            }

            JsonElement? audioStream = null;
            foreach (var stream in streams.EnumerateArray())
            {
                if (stream.TryGetProperty("codec_type", out var codecType) &&
                    codecType.GetString() == "audio")
                {
                    audioStream = stream;
                    break;
                }
            }

            if (!audioStream.HasValue)
            {
                throw new FfprobeException("No audio stream found in file", ProcessingFailureReason.UnsupportedCodec);
            }

            var audio = audioStream.Value;

            // Parse codec
            var codec = audio.TryGetProperty("codec_name", out var codecProp)
                ? codecProp.GetString() ?? "unknown"
                : "unknown";

            var codecLongName = audio.TryGetProperty("codec_long_name", out var codecLongProp)
                ? codecLongProp.GetString() ?? codec
                : codec;

            // Parse sample rate
            var sampleRate = 0;
            if (audio.TryGetProperty("sample_rate", out var sampleRateProp))
            {
                int.TryParse(sampleRateProp.GetString(), out sampleRate);
            }

            // Parse channels
            var channels = audio.TryGetProperty("channels", out var channelsProp)
                ? channelsProp.GetInt32()
                : 0;

            // Parse bit depth (for lossless formats)
            int? bitDepth = null;
            if (audio.TryGetProperty("bits_per_sample", out var bitDepthProp) && bitDepthProp.TryGetInt32(out var bd) && bd > 0)
            {
                bitDepth = bd;
            }
            else if (audio.TryGetProperty("bits_per_raw_sample", out var rawBitDepthProp))
            {
                if (int.TryParse(rawBitDepthProp.GetString(), out var rbd) && rbd > 0)
                {
                    bitDepth = rbd;
                }
            }

            // Parse embedded tags
            string? embeddedTitle = null, embeddedArtist = null, embeddedAlbum = null, embeddedGenre = null;
            int? embeddedYear = null;

            if (format.TryGetProperty("tags", out var tags))
            {
                embeddedTitle = GetTagValue(tags, "title");
                embeddedArtist = GetTagValue(tags, "artist") ?? GetTagValue(tags, "ARTIST");
                embeddedAlbum = GetTagValue(tags, "album") ?? GetTagValue(tags, "ALBUM");
                embeddedGenre = GetTagValue(tags, "genre") ?? GetTagValue(tags, "GENRE");

                var yearStr = GetTagValue(tags, "date") ?? GetTagValue(tags, "year") ?? GetTagValue(tags, "DATE");
                if (yearStr != null && int.TryParse(yearStr.Length >= 4 ? yearStr[..4] : yearStr, out var year))
                {
                    embeddedYear = year;
                }
            }

            // Get file size
            var fileSize = new FileInfo(filePath).Length;

            return new AudioMetadata
            {
                Duration = duration,
                SampleRate = sampleRate,
                Channels = channels,
                BitRate = bitRate,
                Codec = codec,
                CodecLongName = codecLongName,
                BitDepth = bitDepth,
                FileSizeBytes = fileSize,
                MimeType = GetMimeType(codec),
                EmbeddedTitle = embeddedTitle,
                EmbeddedArtist = embeddedArtist,
                EmbeddedAlbum = embeddedAlbum,
                EmbeddedYear = embeddedYear,
                EmbeddedGenre = embeddedGenre
            };
        }
        catch (FfprobeException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse ffprobe JSON output");
            throw new FfprobeException("Failed to parse ffprobe output", ProcessingFailureReason.CorruptedFile, ex);
        }
    }

    private static string? GetTagValue(JsonElement tags, string key)
    {
        if (tags.TryGetProperty(key, out var value))
        {
            return value.GetString();
        }
        return null;
    }

    private static string? GetMimeType(string codec)
    {
        return codec.ToLowerInvariant() switch
        {
            "mp3" => "audio/mpeg",
            "aac" => "audio/aac",
            "flac" => "audio/flac",
            "vorbis" => "audio/ogg",
            "opus" => "audio/opus",
            "wav" or "pcm_s16le" or "pcm_s24le" or "pcm_s32le" => "audio/wav",
            "alac" => "audio/mp4",
            _ => null
        };
    }
}
