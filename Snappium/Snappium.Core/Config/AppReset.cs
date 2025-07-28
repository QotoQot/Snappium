namespace Snappium.Core.Config;

/// <summary>
/// App reset policy configuration.
/// </summary>
public sealed class AppReset
{
    /// <summary>
    /// When to reset app data ("never", "on_language_change", "always").
    /// </summary>
    public string? Policy { get; init; }

    /// <summary>
    /// Whether to clear app data when language changes.
    /// </summary>
    public bool? ClearDataOnLanguageChange { get; init; }

    /// <summary>
    /// Whether to reinstall or relaunch the app ("reinstall", "relaunch").
    /// </summary>
    public string? ReinstallVsRelaunch { get; init; }
}