namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Cursor for stable pagination across track list queries.
/// </summary>
internal record TrackListCursor(
    string SortValue,
    string TrackId,
    DateTimeOffset Timestamp);
