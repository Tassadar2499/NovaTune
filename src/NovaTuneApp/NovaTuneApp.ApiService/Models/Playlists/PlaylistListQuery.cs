namespace NovaTuneApp.ApiService.Models.Playlists;

/// <summary>
/// Query parameters for listing playlists.
/// </summary>
public record PlaylistListQuery(
    string? Search = null,
    string SortBy = "updatedAt",
    string SortOrder = "desc",
    string? Cursor = null,
    int Limit = 20);
