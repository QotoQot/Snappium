namespace Snappium.Core.Config;

/// <summary>
/// Status bar configuration for demo mode on both platforms.
/// </summary>
public sealed class StatusBar
{
    /// <summary>
    /// iOS status bar configuration.
    /// </summary>
    public IosStatusBar? Ios { get; init; }

    /// <summary>
    /// Android status bar configuration.
    /// </summary>
    public AndroidStatusBar? Android { get; init; }
}

/// <summary>
/// iOS-specific status bar configuration for demo mode.
/// </summary>
public sealed class IosStatusBar
{
    /// <summary>
    /// Time to display (e.g., "9:41").
    /// </summary>
    public string? Time { get; init; }

    /// <summary>
    /// Number of Wi-Fi signal bars (0-4).
    /// </summary>
    public int? WifiBars { get; init; }

    /// <summary>
    /// Number of cellular signal bars (0-4).
    /// </summary>
    public int? CellularBars { get; init; }

    /// <summary>
    /// Battery state ("charging", "charged", "unplugged").
    /// </summary>
    public string? BatteryState { get; init; }
}

/// <summary>
/// Android-specific status bar configuration for demo mode.
/// </summary>
public sealed class AndroidStatusBar
{
    /// <summary>
    /// Whether to enable demo mode.
    /// </summary>
    public bool? DemoMode { get; init; }

    /// <summary>
    /// Time to display (e.g., "09:41").
    /// </summary>
    public string? Clock { get; init; }

    /// <summary>
    /// Battery percentage (0-100).
    /// </summary>
    public int? Battery { get; init; }

    /// <summary>
    /// Wi-Fi display mode.
    /// </summary>
    public string? Wifi { get; init; }

    /// <summary>
    /// Notification display mode.
    /// </summary>
    public string? Notifications { get; init; }
}