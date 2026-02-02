using NovaTuneApp.ApiService.Models.Analytics;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// Index for querying track daily aggregates by date range.
/// Used for track analytics queries.
/// </summary>
public class TrackDailyAggregates_ByDateRange : AbstractIndexCreationTask<TrackDailyAggregate>
{
    public TrackDailyAggregates_ByDateRange()
    {
        Map = aggregates => from agg in aggregates
                            select new
                            {
                                agg.TrackId,
                                agg.UserId,
                                agg.DateBucket,
                                agg.TotalPlays,
                                agg.CompletedPlays,
                                agg.TotalSecondsPlayed
                            };
    }
}
