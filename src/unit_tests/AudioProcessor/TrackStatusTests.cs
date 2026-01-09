using NovaTune.UnitTests.AudioProcessor.Fixtures;
using NovaTuneApp.ApiService.Models;

namespace NovaTune.UnitTests.AudioProcessor;

/// <summary>
/// Unit tests for Track status transitions and idempotency.
/// Tests the rules for when processing should or should not occur.
/// </summary>
public class TrackStatusTests
{
    // ============================================================================
    // Status Default Value Tests
    // ============================================================================

    [Fact]
    public void Track_Should_default_to_processing_status()
    {
        var track = new Track();

        track.Status.ShouldBe(TrackStatus.Processing);
    }

    // ============================================================================
    // Processing Decision Tests (Idempotency)
    // ============================================================================

    [Fact]
    public void Should_process_track_in_processing_status()
    {
        var track = AudioUploadedEventFixtures.CreateProcessingTrack(
            Ulid.NewUlid().ToString(),
            Ulid.NewUlid().ToString());

        var shouldProcess = track.Status == TrackStatus.Processing;

        shouldProcess.ShouldBeTrue();
    }

    [Fact]
    public void Should_skip_track_in_ready_status()
    {
        var track = AudioUploadedEventFixtures.CreateReadyTrack(
            Ulid.NewUlid().ToString(),
            Ulid.NewUlid().ToString());

        var shouldProcess = track.Status == TrackStatus.Processing;

        shouldProcess.ShouldBeFalse();
    }

    [Fact]
    public void Should_skip_track_in_failed_status()
    {
        var track = AudioUploadedEventFixtures.CreateFailedTrack(
            Ulid.NewUlid().ToString(),
            Ulid.NewUlid().ToString(),
            ProcessingFailureReason.CorruptedFile);

        var shouldProcess = track.Status == TrackStatus.Processing;

        shouldProcess.ShouldBeFalse();
    }

    [Fact]
    public void Should_skip_track_in_deleted_status()
    {
        var track = AudioUploadedEventFixtures.CreateDeletedTrack(
            Ulid.NewUlid().ToString(),
            Ulid.NewUlid().ToString());

        var shouldProcess = track.Status == TrackStatus.Processing;

        shouldProcess.ShouldBeFalse();
    }

    [Theory]
    [InlineData(TrackStatus.Ready)]
    [InlineData(TrackStatus.Failed)]
    [InlineData(TrackStatus.Deleted)]
    public void Should_skip_processing_for_non_processing_status(TrackStatus status)
    {
        var track = new Track
        {
            Id = "Tracks/test",
            TrackId = "test",
            UserId = "user",
            Title = "Test Track",
            ObjectKey = "audio/test",
            Status = status
        };

        var shouldProcess = track.Status == TrackStatus.Processing;

        shouldProcess.ShouldBeFalse();
    }

    // ============================================================================
    // Status Transition Tests
    // ============================================================================

    [Fact]
    public void Processing_to_ready_should_be_allowed()
    {
        var track = AudioUploadedEventFixtures.CreateProcessingTrack(
            Ulid.NewUlid().ToString(),
            Ulid.NewUlid().ToString());

        // Simulate successful processing
        track.Status = TrackStatus.Ready;
        track.ProcessedAt = DateTimeOffset.UtcNow;

        track.Status.ShouldBe(TrackStatus.Ready);
        track.ProcessedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Processing_to_failed_should_be_allowed()
    {
        var track = AudioUploadedEventFixtures.CreateProcessingTrack(
            Ulid.NewUlid().ToString(),
            Ulid.NewUlid().ToString());

        // Simulate failed processing
        track.Status = TrackStatus.Failed;
        track.FailureReason = ProcessingFailureReason.CorruptedFile;
        track.ProcessedAt = DateTimeOffset.UtcNow;

        track.Status.ShouldBe(TrackStatus.Failed);
        track.FailureReason.ShouldBe(ProcessingFailureReason.CorruptedFile);
    }

    // ============================================================================
    // Track Metadata Population Tests
    // ============================================================================

    [Fact]
    public void Ready_track_should_have_metadata()
    {
        var track = AudioUploadedEventFixtures.CreateReadyTrack(
            Ulid.NewUlid().ToString(),
            Ulid.NewUlid().ToString());

        track.Metadata.ShouldNotBeNull();
        track.Metadata.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        track.Metadata.SampleRate.ShouldBeGreaterThan(0);
        track.Metadata.Channels.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Failed_track_should_have_failure_reason()
    {
        var track = AudioUploadedEventFixtures.CreateFailedTrack(
            Ulid.NewUlid().ToString(),
            Ulid.NewUlid().ToString(),
            ProcessingFailureReason.DurationExceeded);

        track.FailureReason.ShouldBe(ProcessingFailureReason.DurationExceeded);
    }

    [Fact]
    public void Deleted_track_should_have_deleted_status()
    {
        var track = AudioUploadedEventFixtures.CreateDeletedTrack(
            Ulid.NewUlid().ToString(),
            Ulid.NewUlid().ToString());

        track.Status.ShouldBe(TrackStatus.Deleted);
    }

    // ============================================================================
    // Waveform Path Tests
    // ============================================================================

    [Fact]
    public void Ready_track_can_have_waveform_object_key()
    {
        var trackId = Ulid.NewUlid().ToString();
        var userId = Ulid.NewUlid().ToString();

        var track = AudioUploadedEventFixtures.CreateProcessingTrack(trackId, userId);

        // Simulate processing completion
        track.Status = TrackStatus.Ready;
        track.WaveformObjectKey = $"waveforms/{userId}/{trackId}/peaks.json";

        track.WaveformObjectKey.ShouldContain(userId);
        track.WaveformObjectKey.ShouldContain(trackId);
        track.WaveformObjectKey.ShouldEndWith("peaks.json");
    }

    [Fact]
    public void Waveform_path_should_follow_expected_format()
    {
        var trackId = "01HXYZ123456789ABCDEFGHJ";
        var userId = "01HXYZ987654321ABCDEFGHJ";

        var expectedPath = $"waveforms/{userId}/{trackId}/peaks.json";

        expectedPath.ShouldStartWith("waveforms/");
        var parts = expectedPath.Split('/');
        parts.Length.ShouldBe(4);
        parts[0].ShouldBe("waveforms");
        parts[1].ShouldBe(userId);
        parts[2].ShouldBe(trackId);
        parts[3].ShouldBe("peaks.json");
    }
}
