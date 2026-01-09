namespace NovaTune.UnitTests.AudioProcessor.Fixtures;

/// <summary>
/// Test fixtures containing sample ffprobe JSON outputs for testing.
/// </summary>
public static class FfprobeOutputFixtures
{
    /// <summary>
    /// Valid ffprobe output for a standard MP3 file.
    /// </summary>
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

    /// <summary>
    /// Valid ffprobe output for a FLAC file.
    /// </summary>
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

    /// <summary>
    /// Valid ffprobe output for an AAC file.
    /// </summary>
    public const string ValidAacOutput = """
        {
          "format": {
            "duration": "300.000000",
            "bit_rate": "256000",
            "size": "9600000"
          },
          "streams": [{
            "codec_type": "audio",
            "codec_name": "aac",
            "codec_long_name": "AAC (Advanced Audio Coding)",
            "sample_rate": "44100",
            "channels": 2,
            "bit_rate": "256000"
          }]
        }
        """;

    /// <summary>
    /// Malformed JSON that cannot be parsed.
    /// </summary>
    public const string MalformedJson = "{ invalid json }";

    /// <summary>
    /// Valid JSON with empty streams array.
    /// </summary>
    public const string EmptyStreams = """
        {
          "format": { "duration": "180.0" },
          "streams": []
        }
        """;

    /// <summary>
    /// Valid JSON with missing duration field.
    /// </summary>
    public const string MissingDuration = """
        {
          "format": { "bit_rate": "320000" },
          "streams": [{ "codec_name": "mp3", "sample_rate": "44100", "channels": 2 }]
        }
        """;

    /// <summary>
    /// JSON output with video stream instead of audio.
    /// </summary>
    public const string VideoStream = """
        {
          "format": { "duration": "180.0" },
          "streams": [{ "codec_type": "video", "codec_name": "h264" }]
        }
        """;

    /// <summary>
    /// JSON with zero duration.
    /// </summary>
    public const string ZeroDuration = """
        {
          "format": { "duration": "0.0" },
          "streams": [{ "codec_type": "audio", "codec_name": "mp3", "sample_rate": "44100", "channels": 2 }]
        }
        """;

    /// <summary>
    /// JSON with very long duration (3 hours).
    /// </summary>
    public const string LongDuration = """
        {
          "format": {
            "duration": "10800.000000",
            "bit_rate": "320000"
          },
          "streams": [{
            "codec_type": "audio",
            "codec_name": "mp3",
            "sample_rate": "44100",
            "channels": 2
          }]
        }
        """;

    /// <summary>
    /// JSON with mono audio.
    /// </summary>
    public const string MonoAudio = """
        {
          "format": {
            "duration": "120.000000",
            "bit_rate": "128000"
          },
          "streams": [{
            "codec_type": "audio",
            "codec_name": "mp3",
            "sample_rate": "44100",
            "channels": 1
          }]
        }
        """;

    /// <summary>
    /// JSON with 5.1 surround sound (6 channels).
    /// </summary>
    public const string SurroundSound = """
        {
          "format": {
            "duration": "180.000000",
            "bit_rate": "1536000"
          },
          "streams": [{
            "codec_type": "audio",
            "codec_name": "ac3",
            "codec_long_name": "ATSC A/52A (AC-3)",
            "sample_rate": "48000",
            "channels": 6
          }]
        }
        """;
}
