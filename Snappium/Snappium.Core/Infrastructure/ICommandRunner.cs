using System.Diagnostics;

namespace Snappium.Core.Infrastructure;

/// <summary>
/// Interface for running shell commands with timeout and output capture.
/// </summary>
public interface ICommandRunner
{
    /// <summary>
    /// Execute a command with the specified arguments.
    /// </summary>
    /// <param name="command">Command to execute</param>
    /// <param name="arguments">Command arguments</param>
    /// <param name="workingDirectory">Working directory for the command</param>
    /// <param name="timeout">Timeout for command execution</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command result with exit code and output</returns>
    Task<CommandResult> RunAsync(
        string command,
        string[]? arguments = null,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a command with retry logic for flaky operations.
    /// </summary>
    /// <param name="command">Command to execute</param>
    /// <param name="arguments">Command arguments</param>
    /// <param name="workingDirectory">Working directory for the command</param>
    /// <param name="timeout">Timeout for command execution</param>
    /// <param name="retryCount">Number of retry attempts</param>
    /// <param name="retryDelay">Delay between retry attempts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command result with exit code and output</returns>
    Task<CommandResult> RunWithRetryAsync(
        string command,
        string[]? arguments = null,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        int retryCount = 3,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a command execution.
/// </summary>
public sealed record CommandResult
{
    /// <summary>
    /// Exit code of the command.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Standard output from the command.
    /// </summary>
    public required string StandardOutput { get; init; }

    /// <summary>
    /// Standard error from the command.
    /// </summary>
    public required string StandardError { get; init; }

    /// <summary>
    /// Total execution time.
    /// </summary>
    public required TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Whether the command executed successfully (exit code 0).
    /// </summary>
    public bool IsSuccess => ExitCode == 0;

    /// <summary>
    /// Combined output from stdout and stderr.
    /// </summary>
    public string CombinedOutput => string.IsNullOrEmpty(StandardError) 
        ? StandardOutput 
        : $"{StandardOutput}\n{StandardError}";
}