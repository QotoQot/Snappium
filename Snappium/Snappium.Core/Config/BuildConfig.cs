namespace Snappium.Core.Config;

/// <summary>
/// Build configuration for app compilation and artifact discovery.
/// </summary>
public sealed class BuildConfig
{
    /// <summary>
    /// iOS build configuration.
    /// </summary>
    public PlatformBuildConfig? Ios { get; init; }

    /// <summary>
    /// Android build configuration.
    /// </summary>
    public PlatformBuildConfig? Android { get; init; }
}

/// <summary>
/// Platform-specific build configuration.
/// </summary>
public sealed class PlatformBuildConfig
{
    /// <summary>
    /// Path to the .csproj file to build.
    /// </summary>
    public string? Csproj { get; init; }

    /// <summary>
    /// Target framework moniker (e.g., "net9.0-ios").
    /// </summary>
    public string? Tfm { get; init; }

    /// <summary>
    /// Glob pattern for discovering build artifacts.
    /// </summary>
    public string? ArtifactGlob { get; init; }

    /// <summary>
    /// Package name/bundle identifier (for Android).
    /// </summary>
    public string? Package { get; init; }
}