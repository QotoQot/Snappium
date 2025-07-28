namespace Snappium.Core.Config;

/// <summary>
/// Platform-specific Appium capability overrides.
/// </summary>
public sealed class Capabilities
{
    /// <summary>
    /// iOS-specific capability overrides.
    /// </summary>
    public Dictionary<string, object>? Ios { get; init; }

    /// <summary>
    /// Android-specific capability overrides.
    /// </summary>
    public Dictionary<string, object>? Android { get; init; }
}

/// <summary>
/// Configuration for automatic popup dismissors.
/// </summary>
public sealed class Dismissors
{
    /// <summary>
    /// iOS popup dismissor selectors.
    /// </summary>
    public List<Selector>? Ios { get; init; }

    /// <summary>
    /// Android popup dismissor selectors.
    /// </summary>
    public List<Selector>? Android { get; init; }
}