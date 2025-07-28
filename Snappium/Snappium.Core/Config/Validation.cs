namespace Snappium.Core.Config;

/// <summary>
/// Screenshot validation configuration.
/// </summary>
public sealed class Validation
{
    /// <summary>
    /// Whether to enforce screenshot dimension validation.
    /// </summary>
    public bool? EnforceImageSize { get; init; }

    /// <summary>
    /// Expected screenshot dimensions by platform and device.
    /// </summary>
    public ExpectedSizes? ExpectedSizes { get; init; }
}

/// <summary>
/// Container for expected screenshot dimensions by platform.
/// </summary>
public sealed class ExpectedSizes
{
    /// <summary>
    /// iOS device size configurations.
    /// </summary>
    public Dictionary<string, DeviceSize>? Ios { get; init; }

    /// <summary>
    /// Android device size configurations.
    /// </summary>
    public Dictionary<string, DeviceSize>? Android { get; init; }
}

/// <summary>
/// Expected screenshot dimensions for a specific device.
/// </summary>
public sealed class DeviceSize
{
    /// <summary>
    /// Expected dimensions in portrait mode [width, height].
    /// </summary>
    public int[]? Portrait { get; init; }

    /// <summary>
    /// Expected dimensions in landscape mode [width, height].
    /// </summary>
    public int[]? Landscape { get; init; }
}