using System.ComponentModel.DataAnnotations;

namespace Snappium.Core.Config;

/// <summary>
/// Root configuration object that represents the complete screenshot automation configuration.
/// </summary>
public sealed class RootConfig
{
    /// <summary>
    /// Device configuration for iOS and Android platforms.
    /// </summary>
    [Required]
    public required Devices Devices { get; init; }

    /// <summary>
    /// List of language codes to process (e.g., "en-US", "es-ES").
    /// </summary>
    [Required]
    public required List<string> Languages { get; init; }

    /// <summary>
    /// Mapping of language codes to platform-specific locale strings.
    /// </summary>
    [Required]
    public required Dictionary<string, LocaleMapping> LocaleMapping { get; init; }

    /// <summary>
    /// List of screenshot plans defining what screenshots to capture and how.
    /// </summary>
    [Required]
    public required List<ScreenshotPlan> Screenshots { get; init; }

    /// <summary>
    /// Build configuration for app compilation and artifact discovery.
    /// </summary>
    public BuildConfig? BuildConfig { get; init; }

    /// <summary>
    /// Timeout configuration for various operations.
    /// </summary>
    public Timeouts? Timeouts { get; init; }

    /// <summary>
    /// Port allocation configuration for parallel execution.
    /// </summary>
    public Ports? Ports { get; init; }

    /// <summary>
    /// App reset policy configuration.
    /// </summary>
    public AppReset? AppReset { get; init; }

    /// <summary>
    /// Failure artifact collection configuration.
    /// </summary>
    public FailureArtifacts? FailureArtifacts { get; init; }

    /// <summary>
    /// Status bar configuration for demo mode.
    /// </summary>
    public StatusBar? StatusBar { get; init; }

    /// <summary>
    /// Screenshot validation configuration.
    /// </summary>
    public Validation? Validation { get; init; }

    /// <summary>
    /// Platform-specific Appium capability overrides.
    /// </summary>
    public Capabilities? Capabilities { get; init; }

    /// <summary>
    /// Popup dismissor configuration for handling dialogs.
    /// </summary>
    public Dismissors? Dismissors { get; init; }
}

/// <summary>
/// Mapping of a language code to platform-specific locale strings.
/// </summary>
public sealed class LocaleMapping
{
    /// <summary>
    /// iOS locale string (e.g., "en_US").
    /// </summary>
    [Required]
    public required string Ios { get; init; }

    /// <summary>
    /// Android locale string (e.g., "en_US").
    /// </summary>
    [Required]
    public required string Android { get; init; }
}