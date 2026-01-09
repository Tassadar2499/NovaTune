using NovaTune.UnitTests.AudioProcessor.Fixtures;
using NovaTuneApp.ApiService.Models;

namespace NovaTune.UnitTests.AudioProcessor;

/// <summary>
/// Unit tests for audio metadata validation logic.
/// Tests the validation rules enforced during audio processing.
/// </summary>
public class MetadataValidationTests
{
    // Validation rules from AudioProcessorService
    private const int MaxTrackDurationMinutes = 120;
    private const int MaxChannels = 8;

    private static readonly HashSet<string> SupportedCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp3", "aac", "flac", "vorbis", "opus", "alac",
        "wav", "pcm_s16le", "pcm_s24le", "pcm_s32le", "pcm_f32le"
    };

    // ============================================================================
    // Duration Validation Tests
    // ============================================================================

    [Fact]
    public void Should_accept_valid_duration()
    {
        var metadata = AudioUploadedEventFixtures.CreateValidMetadata();

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_reject_zero_duration()
    {
        var metadata = AudioUploadedEventFixtures.CreateZeroDurationMetadata();

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.InvalidDuration);
    }

    [Fact]
    public void Should_reject_negative_duration()
    {
        var metadata = CreateMetadata(duration: TimeSpan.FromSeconds(-10));

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.InvalidDuration);
    }

    [Fact]
    public void Should_reject_duration_exceeding_limit()
    {
        var metadata = AudioUploadedEventFixtures.CreateExcessiveDurationMetadata();

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.DurationExceeded);
    }

    [Fact]
    public void Should_accept_duration_at_limit()
    {
        var metadata = CreateMetadata(duration: TimeSpan.FromMinutes(MaxTrackDurationMinutes));

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_reject_duration_just_over_limit()
    {
        var metadata = CreateMetadata(duration: TimeSpan.FromMinutes(MaxTrackDurationMinutes + 1));

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.DurationExceeded);
    }

    // ============================================================================
    // Sample Rate Validation Tests
    // ============================================================================

    [Fact]
    public void Should_reject_zero_sample_rate()
    {
        var metadata = CreateMetadata(sampleRate: 0);

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.InvalidSampleRate);
    }

    [Fact]
    public void Should_reject_negative_sample_rate()
    {
        var metadata = CreateMetadata(sampleRate: -44100);

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.InvalidSampleRate);
    }

    [Theory]
    [InlineData(8000)]    // Low quality
    [InlineData(22050)]   // Half CD quality
    [InlineData(44100)]   // CD quality
    [InlineData(48000)]   // Standard
    [InlineData(96000)]   // High resolution
    [InlineData(192000)]  // Studio quality
    public void Should_accept_valid_sample_rates(int sampleRate)
    {
        var metadata = CreateMetadata(sampleRate: sampleRate);

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeTrue();
    }

    // ============================================================================
    // Channel Validation Tests
    // ============================================================================

    [Fact]
    public void Should_reject_zero_channels()
    {
        var metadata = AudioUploadedEventFixtures.CreateInvalidChannelsMetadata();

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.InvalidChannels);
    }

    [Fact]
    public void Should_reject_negative_channels()
    {
        var metadata = CreateMetadata(channels: -1);

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.InvalidChannels);
    }

    [Fact]
    public void Should_reject_excessive_channels()
    {
        var metadata = CreateMetadata(channels: 10); // Exceeds max of 8

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.InvalidChannels);
    }

    [Theory]
    [InlineData(1)]  // Mono
    [InlineData(2)]  // Stereo
    [InlineData(6)]  // 5.1 surround
    [InlineData(8)]  // 7.1 surround (max allowed)
    public void Should_accept_valid_channel_counts(int channels)
    {
        var metadata = CreateMetadata(channels: channels);

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeTrue();
    }

    // ============================================================================
    // Codec Validation Tests
    // ============================================================================

    [Fact]
    public void Should_reject_whitespace_codec()
    {
        var metadata = CreateMetadata(codec: "   ");

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.UnsupportedCodec);
    }

    [Fact]
    public void Should_reject_empty_codec()
    {
        var metadata = CreateMetadata(codec: "");

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.UnsupportedCodec);
    }

    [Fact]
    public void Should_reject_unsupported_codec()
    {
        var metadata = AudioUploadedEventFixtures.CreateUnsupportedCodecMetadata();

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.UnsupportedCodec);
    }

    [Theory]
    [InlineData("mp3")]
    [InlineData("aac")]
    [InlineData("flac")]
    [InlineData("vorbis")]
    [InlineData("opus")]
    [InlineData("alac")]
    [InlineData("wav")]
    [InlineData("pcm_s16le")]
    [InlineData("pcm_s24le")]
    [InlineData("pcm_s32le")]
    [InlineData("pcm_f32le")]
    public void Should_accept_supported_codecs(string codec)
    {
        var metadata = CreateMetadata(codec: codec);

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("MP3")]
    [InlineData("AAC")]
    [InlineData("FLAC")]
    [InlineData("Vorbis")]
    public void Should_accept_codecs_case_insensitively(string codec)
    {
        var metadata = CreateMetadata(codec: codec);

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("h264")]
    [InlineData("hevc")]
    [InlineData("vp9")]
    [InlineData("av1")]
    [InlineData("mpeg4")]
    public void Should_reject_video_codecs(string codec)
    {
        var metadata = CreateMetadata(codec: codec);

        var result = ValidateMetadata(metadata);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe(ProcessingFailureReason.UnsupportedCodec);
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    /// <summary>
    /// Creates metadata with customizable properties.
    /// </summary>
    private static AudioMetadata CreateMetadata(
        TimeSpan? duration = null,
        int? sampleRate = null,
        int? channels = null,
        string? codec = null)
    {
        return new AudioMetadata
        {
            Duration = duration ?? TimeSpan.FromMinutes(3),
            SampleRate = sampleRate ?? 44100,
            Channels = channels ?? 2,
            BitRate = 320000,
            Codec = codec ?? "mp3",
            CodecLongName = "MP3 (MPEG audio layer 3)"
        };
    }

    private record ValidationResult(bool IsValid, string? FailureReason)
    {
        public static ValidationResult Success() => new(true, null);
        public static ValidationResult Failed(string reason) => new(false, reason);
    }

    private ValidationResult ValidateMetadata(AudioMetadata metadata)
    {
        // Duration validation
        if (metadata.Duration <= TimeSpan.Zero)
        {
            return ValidationResult.Failed(ProcessingFailureReason.InvalidDuration);
        }

        if (metadata.Duration > TimeSpan.FromMinutes(MaxTrackDurationMinutes))
        {
            return ValidationResult.Failed(ProcessingFailureReason.DurationExceeded);
        }

        // Sample rate validation
        if (metadata.SampleRate <= 0)
        {
            return ValidationResult.Failed(ProcessingFailureReason.InvalidSampleRate);
        }

        // Channel count validation (1-8)
        if (metadata.Channels < 1 || metadata.Channels > MaxChannels)
        {
            return ValidationResult.Failed(ProcessingFailureReason.InvalidChannels);
        }

        // Codec validation - must be a recognized audio codec
        if (string.IsNullOrEmpty(metadata.Codec) || !SupportedCodecs.Contains(metadata.Codec))
        {
            return ValidationResult.Failed(ProcessingFailureReason.UnsupportedCodec);
        }

        return ValidationResult.Success();
    }
}
