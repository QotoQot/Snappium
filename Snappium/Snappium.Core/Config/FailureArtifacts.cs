namespace Snappium.Core.Config;

/// <summary>
/// Configuration for collecting failure artifacts for debugging.
/// </summary>
public sealed class FailureArtifacts
{
    /// <summary>
    /// Whether to save page source XML on failure.
    /// </summary>
    public bool? SavePageSource { get; init; }

    /// <summary>
    /// Whether to save a screenshot on failure.
    /// </summary>
    public bool? SaveScreenshot { get; init; }

    /// <summary>
    /// Whether to save Appium logs on failure.
    /// </summary>
    public bool? SaveAppiumLogs { get; init; }

    /// <summary>
    /// Whether to save device logs on failure.
    /// </summary>
    public bool? SaveDeviceLogs { get; init; }

    /// <summary>
    /// Directory to save artifacts to.
    /// </summary>
    public string? ArtifactsDir { get; init; }
}