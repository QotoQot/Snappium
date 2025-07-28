using System.ComponentModel.DataAnnotations;

namespace Snappium.Core.Config;

/// <summary>
/// Container for iOS and Android device configurations.
/// </summary>
public sealed class Devices
{
    /// <summary>
    /// iOS device/simulator configurations.
    /// </summary>
    [Required]
    public required List<IosDevice> Ios { get; init; }

    /// <summary>
    /// Android device/emulator configurations.
    /// </summary>
    [Required]
    public required List<AndroidDevice> Android { get; init; }
}

/// <summary>
/// iOS device/simulator configuration.
/// </summary>
public sealed class IosDevice
{
    /// <summary>
    /// Display name of the device (e.g., "iPhone 15 Pro Max").
    /// </summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>
    /// Specific simulator UDID to use (optional, uses name matching if null).
    /// </summary>
    public string? Udid { get; init; }

    /// <summary>
    /// Folder name for organizing screenshots (must be unique across platforms).
    /// </summary>
    [Required]
    public required string Folder { get; init; }

    /// <summary>
    /// iOS platform version (e.g., "17.5").
    /// </summary>
    [Required]
    public required string PlatformVersion { get; init; }
}

/// <summary>
/// Android device/emulator configuration.
/// </summary>
public sealed class AndroidDevice
{
    /// <summary>
    /// Display name of the device (e.g., "Pixel 7 Pro").
    /// </summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>
    /// Android Virtual Device (AVD) name.
    /// </summary>
    [Required]
    public required string Avd { get; init; }

    /// <summary>
    /// Folder name for organizing screenshots (must be unique across platforms).
    /// </summary>
    [Required]
    public required string Folder { get; init; }

    /// <summary>
    /// Android platform version/API level (e.g., "14").
    /// </summary>
    [Required]
    public required string PlatformVersion { get; init; }
}