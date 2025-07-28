namespace Snappium.Core.Build;

/// <summary>
/// Service for checking required dependencies and tools.
/// </summary>
public interface IDependencyChecker
{
    /// <summary>
    /// Check all required dependencies for the current platform.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dependency check result</returns>
    Task<DependencyCheckResult> CheckDependenciesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if .NET CLI is available and get version.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dependency check result for .NET</returns>
    Task<DependencyResult> CheckDotNetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if xcrun (Xcode Command Line Tools) is available on macOS.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dependency check result for xcrun</returns>
    Task<DependencyResult> CheckXcrunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if Android Debug Bridge (adb) is available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dependency check result for adb</returns>
    Task<DependencyResult> CheckAdbAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if Appium is available (optional).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dependency check result for Appium</returns>
    Task<DependencyResult> CheckAppiumAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of checking all dependencies.
/// </summary>
public sealed record DependencyCheckResult
{
    /// <summary>
    /// Whether all required dependencies are available.
    /// </summary>
    public required bool AllRequiredAvailable { get; init; }

    /// <summary>
    /// Individual dependency results.
    /// </summary>
    public required List<DependencyResult> Dependencies { get; init; }

    /// <summary>
    /// Warning messages for missing optional dependencies.
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Result of checking a single dependency.
/// </summary>
public sealed record DependencyResult
{
    /// <summary>
    /// Name of the dependency.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether the dependency is available.
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// Whether this dependency is required (vs optional).
    /// </summary>
    public required bool IsRequired { get; init; }

    /// <summary>
    /// Version information if available.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Path to the executable if found.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Error message if dependency check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}