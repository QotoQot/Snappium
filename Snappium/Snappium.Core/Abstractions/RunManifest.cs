namespace Snappium.Core.Abstractions;

/// <summary>
/// Complete manifest of a screenshot automation run.
/// </summary>
public sealed class RunManifest
{
    /// <summary>
    /// Information about the run itself.
    /// </summary>
    public required RunInfo RunInfo { get; init; }

    /// <summary>
    /// Results of all jobs executed in this run.
    /// </summary>
    public required List<JobResult> Jobs { get; init; }

    /// <summary>
    /// Summary statistics for the run.
    /// </summary>
    public required RunSummary Summary { get; init; }

    /// <summary>
    /// Environment information where the run executed.
    /// </summary>
    public required EnvironmentInfo Environment { get; init; }
}

/// <summary>
/// Information about a screenshot automation run.
/// </summary>
public sealed class RunInfo
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
    /// When the run completed.
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// Total duration of the run.
    /// </summary>
    public TimeSpan? TotalDuration => EndTime - StartTime;

    /// <summary>
    /// Configuration file used for this run.
    /// </summary>
    public required string ConfigFile { get; init; }

    /// <summary>
    /// Version of the automation tool.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Overall status of the run.
    /// </summary>
    public required RunStatus Status { get; init; }

    /// <summary>
    /// Error message if the run failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Summary statistics for a run.
/// </summary>
public sealed class RunSummary
{
    /// <summary>
    /// Total number of jobs executed.
    /// </summary>
    public required int TotalJobs { get; init; }

    /// <summary>
    /// Number of jobs that completed successfully.
    /// </summary>
    public required int SuccessfulJobs { get; init; }

    /// <summary>
    /// Number of jobs that failed.
    /// </summary>
    public required int FailedJobs { get; init; }

    /// <summary>
    /// Success rate as a percentage.
    /// </summary>
    public double SuccessRatePercent => TotalJobs > 0 ? (double)SuccessfulJobs / TotalJobs * 100 : 0;

    /// <summary>
    /// Total number of screenshots captured.
    /// </summary>
    public required int TotalScreenshots { get; init; }

    /// <summary>
    /// Platforms that were processed.
    /// </summary>
    public required List<string> PlatformsProcessed { get; init; }

    /// <summary>
    /// Languages that were processed.
    /// </summary>
    public required List<string> LanguagesProcessed { get; init; }

    /// <summary>
    /// Devices that were processed.
    /// </summary>
    public required List<string> DevicesProcessed { get; init; }
}

/// <summary>
/// Environment information for a run.
/// </summary>
public sealed class EnvironmentInfo
{
    /// <summary>
    /// .NET runtime version.
    /// </summary>
    public required string DotNetVersion { get; init; }

    /// <summary>
    /// Operating system platform.
    /// </summary>
    public required string Platform { get; init; }

    /// <summary>
    /// Working directory where the run executed.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Machine name where the run executed.
    /// </summary>
    public required string MachineName { get; init; }

    /// <summary>
    /// Hash of the configuration for tracking.
    /// </summary>
    public required string ConfigHash { get; init; }
}

/// <summary>
/// Overall status of a run.
/// </summary>
public enum RunStatus
{
    /// <summary>
    /// Run is currently in progress.
    /// </summary>
    Running,

    /// <summary>
    /// Run completed successfully (all jobs succeeded).
    /// </summary>
    Success,

    /// <summary>
    /// Run completed with some failures.
    /// </summary>
    PartialSuccess,

    /// <summary>
    /// Run failed completely.
    /// </summary>
    Failed,

    /// <summary>
    /// Run was cancelled.
    /// </summary>
    Cancelled
}