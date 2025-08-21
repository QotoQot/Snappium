using System.ComponentModel.DataAnnotations;

namespace Snappium.Core.Config;

/// <summary>
/// Configuration for app artifacts (bundles/APKs).
/// </summary>
public sealed class Artifacts
{
    /// <summary>
    /// iOS app configuration.
    /// </summary>
    [Required]
    public required IosArtifact Ios { get; init; }

    /// <summary>
    /// Android app configuration.
    /// </summary>
    [Required]
    public required AndroidArtifact Android { get; init; }
}

/// <summary>
/// iOS app artifact configuration.
/// </summary>
public sealed class IosArtifact
{
    /// <summary>
    /// Glob pattern to find the iOS .app bundle.
    /// </summary>
    [Required]
    public required string ArtifactGlob { get; init; }

    /// <summary>
    /// iOS bundle identifier for the app.
    /// </summary>
    [Required]
    public required string Package { get; init; }
}

/// <summary>
/// Android app artifact configuration.
/// </summary>
public sealed class AndroidArtifact
{
    /// <summary>
    /// Glob pattern to find the Android .apk file.
    /// </summary>
    [Required]
    public required string ArtifactGlob { get; init; }

    /// <summary>
    /// Android package name for the app.
    /// </summary>
    [Required]
    public required string Package { get; init; }
}