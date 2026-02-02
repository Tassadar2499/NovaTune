namespace NovaTuneApp.ApiService.Models.Playlists;

/// <summary>
/// Request to reorder tracks within a playlist.
/// </summary>
/// <param name="Moves">List of move operations (applied sequentially).</param>
public record ReorderRequest(IReadOnlyList<MoveOperation> Moves);
