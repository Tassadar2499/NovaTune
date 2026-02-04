using NovaTuneApp.ApiService.Models.Admin;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for querying and filtering audit log entries.
/// </summary>
public class AuditLogs_ByFilters : AbstractIndexCreationTask<AuditLogEntry>
{
    public AuditLogs_ByFilters()
    {
        Map = entries => from entry in entries
                         select new
                         {
                             entry.ActorUserId,
                             entry.Action,
                             entry.TargetType,
                             entry.TargetId,
                             entry.Timestamp,
                             entry.ReasonCode
                         };
    }
}
