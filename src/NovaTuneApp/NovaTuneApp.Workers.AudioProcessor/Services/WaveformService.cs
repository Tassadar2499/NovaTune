using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using NovaTuneApp.ApiService.Models;

namespace NovaTuneApp.Workers.AudioProcessor.Services;

/// <summary>
/// Implementation of waveform generation using ffmpeg.
/// Generates JSON peak data for audio visualization.
/// </summary>
public class WaveformService : IWaveformService
{
    private readonly ILogger<WaveformService> _logger;
    private readonly AudioProcessorOptions _options;

    public WaveformService(
        ILogger<WaveformService> logger,
        IOptions<AudioProcessorOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task GenerateAsync(string audioFilePath, string outputPath, int peakCount, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Generating waveform for {AudioFilePath} with {PeakCount} peaks", audioFilePath, peakCount);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.FfmpegTimeoutSeconds));

        try
        {
            // Use ffmpeg to extract raw audio samples and compute peaks
            // -i: input file
            // -ac 1: mix to mono
            // -f f32le: output as 32-bit float little-endian
            // -ar: sample rate (we'll use a low rate to get reasonable number of samples)

            var tempRawFile = Path.Combine(Path.GetDirectoryName(outputPath)!, $"{Path.GetFileNameWithoutExtension(outputPath)}.raw");

            try
            {
                // First, get audio duration to calculate samples per peak
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{audioFilePath}\" -ac 1 -f f32le -ar 8000 \"{tempRawFile}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var errorTask = process.StandardError.ReadToEndAsync(cts.Token);
                await process.WaitForExitAsync(cts.Token);

                var error = await errorTask;

                if (process.ExitCode != 0)
                {
                    _logger.LogError("ffmpeg failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                    throw new WaveformException(
                        $"ffmpeg failed with exit code {process.ExitCode}",
                        ProcessingFailureReason.FfmpegTimeout);
                }

                // Read raw samples and compute peaks
                var peaks = await ComputePeaksAsync(tempRawFile, peakCount, cts.Token);

                // Write JSON output per 04-waveform-generation.md schema
                var waveformData = new WaveformData
                {
                    Version = 1,
                    SampleRate = 8000,
                    SamplesPerPeak = (int)Math.Ceiling((double)new FileInfo(tempRawFile).Length / sizeof(float) / peakCount),
                    Peaks = peaks
                };

                var json = JsonSerializer.Serialize(waveformData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                // Enforce 100KB max size limit
                const int maxSizeBytes = 100 * 1024;
                if (json.Length > maxSizeBytes)
                {
                    _logger.LogWarning(
                        "Waveform JSON exceeds {MaxSize}KB limit ({ActualSize} bytes), truncating peaks",
                        100, json.Length);

                    // Reduce peaks to fit within limit (estimate ~10 chars per peak value)
                    var targetPeakCount = (maxSizeBytes - 100) / 10; // Leave room for JSON structure
                    var step = Math.Max(1, peaks.Length / targetPeakCount);
                    var truncatedPeaks = peaks.Where((_, i) => i % step == 0).ToArray();

                    waveformData = waveformData with { Peaks = truncatedPeaks };
                    json = JsonSerializer.Serialize(waveformData, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });
                }

                await File.WriteAllTextAsync(outputPath, json, cts.Token);

                _logger.LogDebug("Generated waveform with {PeakCount} peaks ({Size} bytes) to {OutputPath}",
                    waveformData.Peaks.Length, json.Length, outputPath);
            }
            finally
            {
                // Clean up temp raw file
                if (File.Exists(tempRawFile))
                {
                    try { File.Delete(tempRawFile); }
                    catch { /* ignore cleanup errors */ }
                }
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Waveform generation timed out after {Timeout}s for {FilePath}",
                _options.FfmpegTimeoutSeconds, audioFilePath);
            throw new WaveformException(
                $"Waveform generation timed out after {_options.FfmpegTimeoutSeconds} seconds",
                ProcessingFailureReason.FfmpegTimeout);
        }
        catch (WaveformException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Waveform generation failed for {FilePath}", audioFilePath);
            throw new WaveformException(
                "Waveform generation failed",
                ProcessingFailureReason.UnknownError,
                ex);
        }
    }

    private static async Task<float[]> ComputePeaksAsync(string rawFilePath, int peakCount, CancellationToken ct)
    {
        var fileInfo = new FileInfo(rawFilePath);
        var totalSamples = fileInfo.Length / sizeof(float);

        if (totalSamples == 0)
        {
            return Array.Empty<float>();
        }

        var samplesPerPeak = Math.Max(1, (int)(totalSamples / peakCount));
        var peaks = new List<float>();

        await using var stream = new FileStream(rawFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var buffer = new byte[samplesPerPeak * sizeof(float)];

        while (peaks.Count < peakCount)
        {
            var bytesRead = await stream.ReadAsync(buffer, ct);
            if (bytesRead == 0) break;

            var samplesRead = bytesRead / sizeof(float);
            var maxAbs = 0f;

            for (var i = 0; i < samplesRead; i++)
            {
                var sample = BitConverter.ToSingle(buffer, i * sizeof(float));
                var abs = Math.Abs(sample);
                if (abs > maxAbs) maxAbs = abs;
            }

            peaks.Add(maxAbs);
        }

        // Normalize peaks to 0-1 range
        var maxPeak = peaks.Count > 0 ? peaks.Max() : 1f;
        if (maxPeak > 0)
        {
            for (var i = 0; i < peaks.Count; i++)
            {
                peaks[i] /= maxPeak;
            }
        }

        return peaks.ToArray();
    }

    /// <summary>
    /// Waveform JSON schema per 04-waveform-generation.md.
    /// </summary>
    private record WaveformData
    {
        public int Version { get; init; }
        public int SampleRate { get; init; }
        public int SamplesPerPeak { get; init; }
        public float[] Peaks { get; init; } = [];
    }
}
