namespace NovaTuneApp.ApiService.Models.Playlists;

/// <summary>
/// Represents a single move operation in a reorder request.
/// </summary>
/// <param name="From">Current position of the track (0-based).</param>
/// <param name="To">Target position for the track (0-based).</param>
public record MoveOperation(int From, int To);
