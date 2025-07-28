using Snappium.Core.Config;

namespace Snappium.Core.Abstractions;

/// <summary>
/// Represents a single screenshot automation job for a specific device and language combination.
/// </summary>
public sealed class RunJob
{
    /// <summary>
    /// Target platform for this job.
    /// </summary>
    public required Platform Platform { get; init; }

    /// <summary>
    /// iOS device configuration (null for Android jobs).
    /// </summary>
    public IosDevice? IosDevice { get; init; }

    /// <summary>
    /// Android device configuration (null for iOS jobs).
    /// </summary>
    public AndroidDevice? AndroidDevice { get; init; }

    /// <summary>
    /// Language code for this job (e.g., "en-US").
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// Platform-specific locale mapping for this language.
    /// </summary>
    public required LocaleMapping LocaleMapping { get; init; }

    /// <summary>
    /// Output directory for screenshots from this job.
    /// </summary>
    public required string OutputDirectory { get; init; }

    /// <summary>
    /// Screenshots to capture in this job.
    /// </summary>
    public required List<ScreenshotPlan> Screenshots { get; init; }

    /// <summary>
    /// Port allocation for this job.
    /// </summary>
    public required PortAllocation Ports { get; init; }

    /// <summary>
    /// Path to the app to install and test.
    /// </summary>
    public required string AppPath { get; init; }

    /// <summary>
    /// Display name of the device being tested.
    /// </summary>
    public string DeviceName => Platform switch
    {
        Platform.iOS => IosDevice?.Name ?? "Unknown iOS Device",
        Platform.Android => AndroidDevice?.Name ?? "Unknown Android Device",
        _ => "Unknown Device"
    };

    /// <summary>
    /// Device folder name for organizing outputs.
    /// </summary>
    public string DeviceFolder => Platform switch
    {
        Platform.iOS => IosDevice?.Folder ?? "Unknown",
        Platform.Android => AndroidDevice?.Folder ?? "Unknown",
        _ => "Unknown"
    };
}