namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Generic paged result for cursor-based pagination.
/// </summary>
/// <typeparam name="T">Type of items in the result.</typeparam>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    int TotalCount,
    bool HasMore);
