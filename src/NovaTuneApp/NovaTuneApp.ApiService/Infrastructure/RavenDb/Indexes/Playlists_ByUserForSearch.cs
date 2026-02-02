using NovaTuneApp.ApiService.Models;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for listing and searching playlists by user.
/// Supports full-text search on name field.
/// </summary>
public class Playlists_ByUserForSearch : AbstractIndexCreationTask<Playlist>
{
    public Playlists_ByUserForSearch()
    {
        Map = playlists => playlists.Select(playlist => new
        {
            playlist.UserId,
            playlist.PlaylistId,
            playlist.Name,
            playlist.TrackCount,
            playlist.CreatedAt,
            playlist.UpdatedAt,
            SearchText = playlist.Name
        });

        Index("SearchText", FieldIndexing.Search);
        Analyze("SearchText", "StandardAnalyzer");
    }
}
