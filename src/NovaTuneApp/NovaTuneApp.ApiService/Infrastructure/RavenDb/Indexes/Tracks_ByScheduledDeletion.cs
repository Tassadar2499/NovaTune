using NovaTuneApp.ApiService.Models;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for lifecycle worker to find tracks ready for physical deletion.
/// Queries tracks that are soft-deleted and past their scheduled deletion time.
/// </summary>
public class Tracks_ByScheduledDeletion : AbstractIndexCreationTask<Track>
{
    public Tracks_ByScheduledDeletion()
    {
        Map = tracks => tracks
            .Where(track => track.Status == TrackStatus.Deleted
                         && track.ScheduledDeletionAt != null)
            .Select(track => new
            {
                track.Status,
                track.ScheduledDeletionAt
            });
    }
}
