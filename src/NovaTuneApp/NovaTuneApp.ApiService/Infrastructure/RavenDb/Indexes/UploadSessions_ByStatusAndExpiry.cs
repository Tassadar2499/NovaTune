using NovaTuneApp.ApiService.Models.Upload;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for efficient upload session cleanup queries.
/// Used by UploadSessionCleanupService to find expired sessions.
/// </summary>
public class UploadSessions_ByStatusAndExpiry : AbstractIndexCreationTask<UploadSession>
{
    public UploadSessions_ByStatusAndExpiry()
    {
        Map = sessions => from session in sessions
                          select new
                          {
                              session.Status,
                              session.ExpiresAt,
                              session.CreatedAt
                          };
    }
}
