using System.ComponentModel.DataAnnotations;
using NovaTuneApp.ApiService.Models;

namespace NovaTune.UnitTests.Models;

public class TrackTests
{
    [Fact]
    public void Track_WithValidData_PassesValidation()
    {
        var track = new Track
        {
            Id = "tracks/1",
            UserId = "users/1",
            Title = "Test Track",
            ObjectKey = "audio/test.mp3",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var results = ValidateModel(track);
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Track_WithEmptyTitle_FailsValidation()
    {
        var track = new Track
        {
            Id = "tracks/1",
            UserId = "users/1",
            Title = "",
            ObjectKey = "audio/test.mp3"
        };

        var results = ValidateModel(track);
        results.ShouldContain(r => r.MemberNames.Contains(nameof(Track.Title)));
    }

    [Fact]
    public void Track_DefaultStatus_IsProcessing()
    {
        var track = new Track();
        track.Status.ShouldBe(TrackStatus.Processing);
    }

    [Fact]
    public void Track_WithMetadata_StoresCorrectly()
    {
        var metadata = new AudioMetadata
        {
            Format = "mp3",
            Bitrate = 320000,
            SampleRate = 44100,
            Channels = 2,
            FileSizeBytes = 5_000_000,
            MimeType = "audio/mpeg"
        };

        var track = new Track
        {
            Id = "tracks/1",
            UserId = "users/1",
            Title = "Test Track",
            ObjectKey = "audio/test.mp3",
            Metadata = metadata
        };

        track.Metadata.ShouldNotBeNull();
        track.Metadata.Format.ShouldBe("mp3");
        track.Metadata.Bitrate.ShouldBe(320000);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }
}
