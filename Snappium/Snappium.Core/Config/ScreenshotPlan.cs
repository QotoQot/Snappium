using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Snappium.Core.Config;

/// <summary>
/// Configuration for a single screenshot capture plan.
/// </summary>
public sealed class ScreenshotPlan
{
    /// <summary>
    /// Unique name for this screenshot.
    /// </summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>
    /// Device orientation for this screenshot ("portrait" or "landscape").
    /// </summary>
    public string? Orientation { get; init; }

    /// <summary>
    /// List of actions to execute for this screenshot.
    /// </summary>
    [Required]
    public required List<ScreenshotAction> Actions { get; init; }

    /// <summary>
    /// Platform-specific assertions to validate after actions complete.
    /// </summary>
    public PlatformAssertions? Assert { get; init; }

    /// <summary>
    /// Platform-specific dismissors to handle popups.
    /// </summary>
    public PlatformDismissors? Dismissors { get; init; }
}

/// <summary>
/// Platform-specific dismissor selectors.
/// </summary>
public sealed class PlatformDismissors
{
    /// <summary>
    /// iOS dismissor selectors.
    /// </summary>
    public List<Selector>? Ios { get; init; }

    /// <summary>
    /// Android dismissor selectors.
    /// </summary>
    public List<Selector>? Android { get; init; }
}

/// <summary>
/// Platform-specific assertion selectors.
/// </summary>
public sealed class PlatformAssertions
{
    /// <summary>
    /// iOS assertion selector.
    /// </summary>
    public Selector? Ios { get; init; }

    /// <summary>
    /// Android assertion selector.
    /// </summary>
    public Selector? Android { get; init; }
}

/// <summary>
/// Screenshot action with one of several possible action types.
/// </summary>
public sealed class ScreenshotAction
{
    /// <summary>
    /// Tap action selector.
    /// </summary>
    public Selector? Tap { get; init; }

    /// <summary>
    /// Wait action configuration.
    /// </summary>
    public WaitConfig? Wait { get; init; }

    /// <summary>
    /// Wait for element configuration.
    /// </summary>
    public WaitForConfig? WaitFor { get; init; }

    /// <summary>
    /// Capture screenshot configuration.
    /// </summary>
    public CaptureConfig? Capture { get; init; }
}

/// <summary>
/// Wait action configuration.
/// </summary>
public sealed class WaitConfig
{
    /// <summary>
    /// Number of seconds to wait.
    /// </summary>
    public double? Seconds { get; init; }
}

/// <summary>
/// Wait for element configuration.
/// </summary>
public sealed class WaitForConfig
{
    /// <summary>
    /// Selector for element to wait for.
    /// </summary>
    public required Selector Selector { get; init; }

    /// <summary>
    /// Timeout in seconds.
    /// </summary>
    public int? Timeout { get; init; }
}

/// <summary>
/// Capture screenshot configuration.
/// </summary>
public sealed class CaptureConfig
{
    /// <summary>
    /// Name of the screenshot file (without extension).
    /// </summary>
    public required string Name { get; init; }
}

/// <summary>
/// Element selector configuration.
/// </summary>
public sealed class Selector
{
    /// <summary>
    /// Accessibility identifier (recommended).
    /// </summary>
    [JsonPropertyName("accessibility_id")]
    public string? AccessibilityId { get; init; }

    /// <summary>
    /// iOS class chain selector.
    /// </summary>
    [JsonPropertyName("ios_class_chain")]
    public string? IosClassChain { get; init; }

    /// <summary>
    /// Android UiAutomator selector.
    /// </summary>
    [JsonPropertyName("android_uiautomator")]
    public string? AndroidUiautomator { get; init; }

    /// <summary>
    /// XPath selector (discouraged).
    /// </summary>
    [JsonPropertyName("xpath")]
    public string? Xpath { get; init; }

    /// <summary>
    /// Resource ID selector.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}