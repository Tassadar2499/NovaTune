using NovaTuneApp.ApiService.Models.Analytics;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// Index for querying user activity aggregates.
/// Used for admin dashboard user activity queries.
/// </summary>
public class UserActivityAggregates_ByUser : AbstractIndexCreationTask<UserActivityAggregate>
{
    public UserActivityAggregates_ByUser()
    {
        Map = aggregates => from agg in aggregates
                            select new
                            {
                                agg.UserId,
                                agg.DateBucket,
                                agg.TotalPlays,
                                agg.TotalSecondsPlayed,
                                agg.LastActivityAt
                            };
    }
}
