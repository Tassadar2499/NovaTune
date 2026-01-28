namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Request model for updating track metadata.
/// Only non-null fields will be updated (merge policy).
/// </summary>
public record UpdateTrackRequest(
    string? Title,
    string? Artist);
