using Snappium.Core.Abstractions;
using Snappium.Core.Config;
using Snappium.Core.Planning;

namespace Snappium.Core.Orchestration;

/// <summary>
/// Main orchestrator for screenshot automation workflows.
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// Execute a complete screenshot automation run.
    /// </summary>
    /// <param name="runPlan">Plan containing all jobs to execute</param>
    /// <param name="config">Root configuration</param>
    /// <param name="cliOverrides">CLI overrides for configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Run result with all job results</returns>
    Task<RunResult> ExecuteAsync(
        RunPlan runPlan,
        RootConfig config,
        CliOverrides? cliOverrides = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// CLI overrides for configuration settings.
/// </summary>
public sealed record CliOverrides
{
    /// <summary>
    /// Override for iOS app path.
    /// </summary>
    public string? IosAppPath { get; init; }

    /// <summary>
    /// Override for Android app path.
    /// </summary>
    public string? AndroidAppPath { get; init; }

    /// <summary>
    /// Override for output directory.
    /// </summary>
    public string? OutputDirectory { get; init; }

    /// <summary>
    /// Override for base port.
    /// </summary>
    public int? BasePort { get; init; }


    /// <summary>
    /// Override for Appium server URL.
    /// </summary>
    public string? ServerUrl { get; init; }
}

/// <summary>
/// Result of a complete screenshot automation run.
/// </summary>
public sealed record RunResult
{
    /// <summary>
    /// Unique identifier for this run.
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// When the run started.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// When the run ended.
    /// </summary>
    public required DateTimeOffset EndTime { get; init; }

    /// <summary>
    /// Total duration of the run.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Whether the overall run succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Results from individual job executions.
    /// </summary>
    public required List<JobResult> JobResults { get; init; }

    /// <summary>
    /// Environment information.
    /// </summary>
    public required EnvironmentInfo Environment { get; init; }

    /// <summary>
    /// Run-level error message if the run failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Environment information for the run.
/// </summary>
public sealed record EnvironmentInfo
{
    /// <summary>
    /// Operating system name and version.
    /// </summary>
    public required string OperatingSystem { get; init; }

    /// <summary>
    /// .NET runtime version.
    /// </summary>
    public required string DotNetVersion { get; init; }

    /// <summary>
    /// Machine hostname.
    /// </summary>
    public required string Hostname { get; init; }

    /// <summary>
    /// Current working directory.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Snappium version.
    /// </summary>
    public required string SnappiumVersion { get; init; }
}