using NovaTuneApp.ApiService.Models;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for admin track list with full-text search support.
/// Includes all tracks regardless of status for admin moderation.
/// </summary>
public class Tracks_ForAdminSearch : AbstractIndexCreationTask<Track>
{
    public Tracks_ForAdminSearch()
    {
        Map = tracks => from track in tracks
                        select new
                        {
                            track.TrackId,
                            track.UserId,
                            track.Title,
                            track.Artist,
                            track.Status,
                            track.ModerationStatus,
                            track.CreatedAt,
                            track.ModeratedAt,
                            track.FileSizeBytes,
                            SearchText = new[] { track.Title, track.Artist }
                        };

        Index("SearchText", FieldIndexing.Search);
        Analyze("SearchText", "StandardAnalyzer");
    }
}
