using NovaTuneApp.ApiService.Models.Analytics;
using Raven.Client.Documents.Indexes;

namespace NovaTuneApp.ApiService.Infrastructure.RavenDb.Indexes;

/// <summary>
/// Map-reduce index for getting top tracks by total play count.
/// Used for admin dashboard top tracks query.
/// </summary>
public class TrackDailyAggregates_TopTracks : AbstractIndexCreationTask<TrackDailyAggregate, TrackDailyAggregates_TopTracks.Result>
{
    public class Result
    {
        public string TrackId { get; set; } = string.Empty;
        public int TotalPlays { get; set; }
        public double TotalSecondsPlayed { get; set; }
    }

    public TrackDailyAggregates_TopTracks()
    {
        Map = aggregates => from agg in aggregates
                            select new Result
                            {
                                TrackId = agg.TrackId,
                                TotalPlays = agg.TotalPlays,
                                TotalSecondsPlayed = agg.TotalSecondsPlayed
                            };

        Reduce = results => from result in results
                            group result by result.TrackId into g
                            select new Result
                            {
                                TrackId = g.Key,
                                TotalPlays = g.Sum(x => x.TotalPlays),
                                TotalSecondsPlayed = g.Sum(x => x.TotalSecondsPlayed)
                            };
    }
}
