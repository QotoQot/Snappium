using Microsoft.Extensions.Logging;

namespace Snappium.Core.DeviceManagement;

/// <summary>
/// Common helper utilities for device management operations.
/// </summary>
public static class DeviceHelpers
{
    /// <summary>
    /// Default timeout for device operations.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default polling interval for device status checks.
    /// </summary>
    public static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Poll a condition until it becomes true or timeout is reached.
    /// </summary>
    /// <param name="condition">Condition to check</param>
    /// <param name="timeout">Maximum time to wait</param>
    /// <param name="pollingInterval">Interval between checks</param>
    /// <param name="logger">Logger for debugging</param>
    /// <param name="operationName">Name of the operation being polled</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if condition became true, false if timeout occurred</returns>
    public static async Task<bool> PollUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan? timeout = null,
        TimeSpan? pollingInterval = null,
        ILogger? logger = null,
        string operationName = "operation",
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var effectiveInterval = pollingInterval ?? DefaultPollingInterval;
        var startTime = DateTimeOffset.UtcNow;

        logger?.LogDebug("Starting to poll for {OperationName} with timeout {Timeout} and interval {Interval}",
            operationName, effectiveTimeout, effectiveInterval);

        while (DateTimeOffset.UtcNow - startTime < effectiveTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (await condition())
                {
                    var elapsed = DateTimeOffset.UtcNow - startTime;
                    logger?.LogDebug("Poll condition for {OperationName} succeeded after {Elapsed}",
                        operationName, elapsed);
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Poll condition for {OperationName} threw exception, will retry", operationName);
            }

            await Task.Delay(effectiveInterval, cancellationToken);
        }

        var totalElapsed = DateTimeOffset.UtcNow - startTime;
        logger?.LogWarning("Poll condition for {OperationName} timed out after {Elapsed}",
            operationName, totalElapsed);
        return false;
    }

    /// <summary>
    /// Wait for a specified duration with logging.
    /// </summary>
    /// <param name="delay">Time to wait</param>
    /// <param name="logger">Logger for debugging</param>
    /// <param name="reason">Reason for the delay</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes after the delay</returns>
    public static async Task DelayAsync(
        TimeSpan delay,
        ILogger? logger = null,
        string reason = "operation",
        CancellationToken cancellationToken = default)
    {
        logger?.LogDebug("Waiting {Delay} for {Reason}", delay, reason);
        await Task.Delay(delay, cancellationToken);
    }

    /// <summary>
    /// Validate that a file exists and is accessible.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="fileType">Type of file for error messages</param>
    /// <returns>The validated file path</returns>
    /// <exception cref="FileNotFoundException">Thrown when file doesn't exist</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when file isn't accessible</exception>
    public static string ValidateFilePath(string filePath, string fileType = "file")
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException($"{fileType} path cannot be null or empty", nameof(filePath));
        }

        // Check if path exists as either file or directory (iOS .app bundles are directories)
        if (!File.Exists(filePath) && !Directory.Exists(filePath))
        {
            throw new FileNotFoundException($"{fileType} not found: {filePath}");
        }

        try
        {
            // Test read access - handle both files and directories
            if (File.Exists(filePath))
            {
                using var _ = File.OpenRead(filePath);
            }
            else if (Directory.Exists(filePath))
            {
                // For directories, just test that we can enumerate them
                Directory.EnumerateFileSystemEntries(filePath).Take(1).ToList();
            }
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException($"Cannot access {fileType}: {filePath}");
        }

        return filePath;
    }

    /// <summary>
    /// Ensure a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="directoryPath">Path to the directory</param>
    /// <returns>The validated directory path</returns>
    public static string EnsureDirectoryExists(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        return directoryPath;
    }

    /// <summary>
    /// Format device identifier for use in commands (prefer UDID over name).
    /// </summary>
    /// <param name="udid">Device UDID (can be null)</param>
    /// <param name="name">Device name</param>
    /// <returns>The preferred identifier to use</returns>
    public static string GetDeviceIdentifier(string? udid, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Device name cannot be null or empty", nameof(name));
        }

        // Prefer UDID if available, otherwise use name
        return !string.IsNullOrWhiteSpace(udid) ? udid : name;
    }

}