using NovaTuneApp.ApiService.Models.Outbox;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// RavenDB index for efficient outbox message queries.
/// Used by OutboxProcessorService to poll for pending messages.
/// </summary>
public class OutboxMessages_ByStatus : AbstractIndexCreationTask<OutboxMessage>
{
    public OutboxMessages_ByStatus()
    {
        Map = messages => from message in messages
                          select new
                          {
                              message.Status,
                              message.CreatedAt,
                              message.Attempts
                          };
    }
}
