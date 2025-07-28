namespace Snappium.Core.Build;

/// <summary>
/// Controller for managing local Appium server instances.
/// </summary>
public interface IAppiumServerController
{
    /// <summary>
    /// Start a local Appium server on the specified port.
    /// </summary>
    /// <param name="port">Port to start Appium server on</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Server start result</returns>
    Task<AppiumServerResult> StartServerAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the Appium server running on the specified port.
    /// </summary>
    /// <param name="port">Port the Appium server is running on</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Server stop result</returns>
    Task<AppiumServerResult> StopServerAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an Appium server is running on the specified port.
    /// </summary>
    /// <param name="port">Port to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if server is running</returns>
    Task<bool> IsServerRunningAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kill any process running on the specified port.
    /// </summary>
    /// <param name="port">Port to clear</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if process was killed</returns>
    Task<bool> KillProcessOnPortAsync(int port, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an Appium server operation.
/// </summary>
public sealed record AppiumServerResult
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Server URL if started successfully.
    /// </summary>
    public string? ServerUrl { get; init; }

    /// <summary>
    /// Process ID if server was started.
    /// </summary>
    public int? ProcessId { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}