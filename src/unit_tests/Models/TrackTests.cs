using System.ComponentModel.DataAnnotations;
using NovaTuneApp.ApiService.Models;

namespace NovaTune.UnitTests.Models;

public class TrackTests
{
    [Fact]
    public void Should_pass_with_valid_data()
    {
        var track = new Track
        {
            Id = "Tracks/01HXYZ123456789ABCDEFGHJ",
            TrackId = "01HXYZ123456789ABCDEFGHJ",
            UserId = "01HXYZ987654321ABCDEFGHJ",
            Title = "Test Track",
            ObjectKey = "audio/test.mp3",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var results = ValidateModel(track);
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Should_fail_with_empty_title()
    {
        var track = new Track
        {
            Id = "Tracks/01HXYZ123456789ABCDEFGHJ",
            TrackId = "01HXYZ123456789ABCDEFGHJ",
            UserId = "01HXYZ987654321ABCDEFGHJ",
            Title = "",
            ObjectKey = "audio/test.mp3"
        };

        var results = ValidateModel(track);
        results.ShouldContain(r => r.MemberNames.Contains(nameof(Track.Title)));
    }

    [Fact]
    public void Should_default_to_processing_status()
    {
        var track = new Track();
        track.Status.ShouldBe(TrackStatus.Processing);
    }

    [Fact]
    public void Should_store_metadata_correctly()
    {
        var metadata = new AudioMetadata
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

        var track = new Track
        {
            Id = "Tracks/01HXYZ123456789ABCDEFGHJ",
            TrackId = "01HXYZ123456789ABCDEFGHJ",
            UserId = "01HXYZ987654321ABCDEFGHJ",
            Title = "Test Track",
            ObjectKey = "audio/test.mp3",
            Metadata = metadata
        };

        track.Metadata.ShouldNotBeNull();
        track.Metadata.Codec.ShouldBe("mp3");
        track.Metadata.BitRate.ShouldBe(320000);
        track.Metadata.Duration.ShouldBe(TimeSpan.FromMinutes(3));
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }
}
