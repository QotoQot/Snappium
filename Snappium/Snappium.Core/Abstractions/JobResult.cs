namespace Snappium.Core.Abstractions;

/// <summary>
/// Result of executing a complete RunJob.
/// </summary>
public sealed class JobResult
{
    /// <summary>
    /// The job that was executed.
    /// </summary>
    public required RunJob Job { get; init; }

    /// <summary>
    /// Overall status of the job execution.
    /// </summary>
    public required JobStatus Status { get; init; }

    /// <summary>
    /// Screenshots captured during this job.
    /// </summary>
    public required List<ScreenshotResult> Screenshots { get; init; }

    /// <summary>
    /// When the job started.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// When the job completed.
    /// </summary>
    public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// Total duration of the job.
    /// </summary>
    public TimeSpan? Duration => EndTime - StartTime;

    /// <summary>
    /// Error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception details if the job failed.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Failure artifacts captured when job failed.
    /// </summary>
    public List<FailureArtifact> FailureArtifacts { get; init; } = new();

    /// <summary>
    /// Whether the job succeeded.
    /// </summary>
    public bool Success => Status == JobStatus.Success;
}

/// <summary>
/// Artifact captured when a job fails.
/// </summary>
public sealed record FailureArtifact
{
    /// <summary>
    /// Type of artifact.
    /// </summary>
    public required FailureArtifactType Type { get; init; }

    /// <summary>
    /// Path to the artifact file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// When the artifact was captured.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Size of the artifact file in bytes.
    /// </summary>
    public long? FileSizeBytes { get; init; }
}

/// <summary>
/// Types of failure artifacts.
/// </summary>
public enum FailureArtifactType
{
    /// <summary>
    /// Page source XML from Appium driver.
    /// </summary>
    PageSource,

    /// <summary>
    /// Screenshot taken at time of failure.
    /// </summary>
    Screenshot,

    /// <summary>
    /// Device logs (logcat for Android, simctl logs for iOS).
    /// </summary>
    DeviceLogs
}

/// <summary>
/// Status of a job execution.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job is queued but not started.
    /// </summary>
    Pending,

    /// <summary>
    /// Job is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Job completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// Job failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Job was cancelled.
    /// </summary>
    Cancelled
}