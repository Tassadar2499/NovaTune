using NovaTuneApp.Workers.AudioProcessor.Services;

namespace NovaTune.UnitTests.Fakes;

/// <summary>
/// Fake implementation of IWaveformService for unit testing.
/// </summary>
public class WaveformServiceFake : IWaveformService
{
    /// <summary>
    /// Exception to throw from GenerateAsync.
    /// If set, will be thrown instead of generating waveform.
    /// </summary>
    public WaveformException? ExceptionToThrow { get; set; }

    /// <summary>
    /// List of waveform generation requests.
    /// </summary>
    public List<(string AudioPath, string OutputPath, int PeakCount)> GeneratedWaveforms { get; } = new();

    /// <summary>
    /// Custom callback for GenerateAsync.
    /// </summary>
    public Func<string, string, int, Task>? OnGenerateAsync { get; set; }

    public async Task GenerateAsync(string audioFilePath, string outputPath, int peakCount, CancellationToken cancellationToken)
    {
        GeneratedWaveforms.Add((audioFilePath, outputPath, peakCount));

        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }

        if (OnGenerateAsync != null)
        {
            await OnGenerateAsync(audioFilePath, outputPath, peakCount);
            return;
        }

        // Write a valid waveform JSON file
        var peaks = Enumerable.Range(0, peakCount)
            .Select(_ => Math.Round((Random.Shared.NextDouble() * 2 - 1), 4))
            .ToArray();

        var json = System.Text.Json.JsonSerializer.Serialize(peaks);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }

    /// <summary>
    /// Resets the fake to its initial state.
    /// </summary>
    public void Reset()
    {
        ExceptionToThrow = null;
        GeneratedWaveforms.Clear();
        OnGenerateAsync = null;
    }
}
