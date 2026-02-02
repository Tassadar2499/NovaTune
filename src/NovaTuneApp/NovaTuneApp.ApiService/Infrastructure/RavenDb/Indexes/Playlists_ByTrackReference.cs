using NovaTuneApp.ApiService.Models;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for finding playlists containing a specific track.
/// Used for deletion cascade when tracks are physically deleted.
/// </summary>
public class Playlists_ByTrackReference : AbstractIndexCreationTask<Playlist, Playlists_ByTrackReference.Result>
{
    public class Result
    {
        public string UserId { get; set; } = string.Empty;
        public string PlaylistId { get; set; } = string.Empty;
        public string TrackId { get; set; } = string.Empty;
    }

    public Playlists_ByTrackReference()
    {
        Map = playlists => from playlist in playlists
                           from track in playlist.Tracks
                           select new Result
                           {
                               UserId = playlist.UserId,
                               PlaylistId = playlist.PlaylistId,
                               TrackId = track.TrackId
                           };
    }
}
