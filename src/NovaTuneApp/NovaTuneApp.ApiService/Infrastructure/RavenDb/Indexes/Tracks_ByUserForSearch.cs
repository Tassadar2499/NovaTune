using NovaTuneApp.ApiService.Models;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for listing and searching tracks by user.
/// Supports full-text search on title and artist fields.
/// </summary>
public class Tracks_ByUserForSearch : AbstractIndexCreationTask<Track>
{
    public Tracks_ByUserForSearch()
    {
        Map = tracks => tracks
            .Where(track => track.Status != TrackStatus.Unknown)
            .Select(track => new
            {
                track.UserId,
                track.Status,
                track.Title,
                track.Artist,
                track.CreatedAt,
                track.UpdatedAt,
                track.Duration,
                SearchText = new[] { track.Title, track.Artist }
            });

        Index("SearchText", FieldIndexing.Search);
        Analyze("SearchText", "StandardAnalyzer");
    }
}
