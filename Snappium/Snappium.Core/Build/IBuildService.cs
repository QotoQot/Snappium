using Snappium.Core.Abstractions;

namespace Snappium.Core.Build;

/// <summary>
/// Service for building .NET projects and discovering artifacts.
/// </summary>
public interface IBuildService
{
    /// <summary>
    /// Build a .NET project for the specified platform.
    /// </summary>
    /// <param name="platform">Target platform (iOS or Android)</param>
    /// <param name="csprojPath">Path to the .csproj file</param>
    /// <param name="configuration">Build configuration (Release, Debug)</param>
    /// <param name="targetFramework">Target framework moniker</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Build result with success status and output path</returns>
    Task<BuildResult> BuildAsync(
        Platform platform,
        string csprojPath,
        string configuration = "Release",
        string? targetFramework = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discover artifacts using glob patterns.
    /// </summary>
    /// <param name="searchPattern">Glob pattern to search for artifacts</param>
    /// <param name="baseDirectory">Base directory to search from</param>
    /// <returns>Path to the latest modified artifact, or null if none found</returns>
    Task<string?> DiscoverArtifactAsync(string searchPattern, string? baseDirectory = null);
}

/// <summary>
/// Result of a build operation.
/// </summary>
public sealed record BuildResult
{
    /// <summary>
    /// Whether the build succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Path to the built artifact.
    /// </summary>
    public string? ArtifactPath { get; init; }

    /// <summary>
    /// Build output directory.
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// Error message if build failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Build duration.
    /// </summary>
    public TimeSpan Duration { get; init; }
}