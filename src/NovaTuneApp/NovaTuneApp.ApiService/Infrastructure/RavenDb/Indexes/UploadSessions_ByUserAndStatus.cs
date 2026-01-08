using NovaTuneApp.ApiService.Models.Upload;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for efficient upload session queries by user and status.
/// </summary>
public class UploadSessions_ByUserAndStatus : AbstractIndexCreationTask<UploadSession>
{
    public UploadSessions_ByUserAndStatus()
    {
        Map = sessions => from session in sessions
                          select new
                          {
                              session.UserId,
                              session.Status,
                              session.ExpiresAt,
                              session.ObjectKey
                          };
    }
}
