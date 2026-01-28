namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Query parameters for listing tracks with pagination, filtering, and sorting.
/// </summary>
public record TrackListQuery(
    string? Search = null,
    TrackStatus? Status = null,
    string SortBy = "createdAt",
    string SortOrder = "desc",
    string? Cursor = null,
    int Limit = 20,
    bool IncludeDeleted = false);
