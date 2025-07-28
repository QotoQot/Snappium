using Snappium.Core.Abstractions;
using Snappium.Core.Config;
using Snappium.Core.Planning;

namespace Snappium.Core.Orchestration;

/// <summary>
/// Interface for executing individual screenshot automation jobs.
/// </summary>
public interface IJobExecutor
{
    /// <summary>
    /// Executes a single screenshot automation job.
    /// </summary>
    /// <param name="job">The job to execute</param>
    /// <param name="config">Root configuration</param>
    /// <param name="cliOverrides">CLI command overrides</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The job execution result</returns>
    Task<JobResult> ExecuteAsync(
        RunJob job,
        RootConfig config,
        CliOverrides? cliOverrides = null,
        CancellationToken cancellationToken = default);
}