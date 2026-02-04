namespace NovaTuneApp.ApiService.Models;

/// <summary>
/// Moderation status for tracks (Req 11.2 clarifications).
/// </summary>
public enum ModerationStatus
{
    /// <summary>
    /// Track is visible and streamable.
    /// </summary>
    None = 0,

    /// <summary>
    /// Track flagged for review, still accessible.
    /// </summary>
    UnderReview = 1,

    /// <summary>
    /// Track disabled by admin, not streamable but retained.
    /// </summary>
    Disabled = 2,

    /// <summary>
    /// Track removed by admin (triggers deletion flow).
    /// </summary>
    Removed = 3
}
