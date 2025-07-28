using Microsoft.Extensions.Logging;
using System.Xml.Linq;

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

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"{fileType} not found: {filePath}");
        }

        try
        {
            // Test read access
            using var _ = File.OpenRead(filePath);
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

    /// <summary>
    /// Extract the bundle ID from an iOS app path using proper XML parsing.
    /// </summary>
    /// <param name="appPath">Path to the .app bundle</param>
    /// <returns>Bundle identifier</returns>
    public static async Task<string> ExtractIosBundleIdAsync(string appPath)
    {
        ValidateFilePath(appPath, "iOS app");

        var infoPlistPath = Path.Combine(appPath, "Info.plist");
        if (!File.Exists(infoPlistPath))
        {
            throw new InvalidOperationException($"Info.plist not found in app bundle: {appPath}");
        }

        try
        {
            // Use proper XML parsing instead of brittle string manipulation
            var content = await File.ReadAllTextAsync(infoPlistPath);
            var doc = XDocument.Parse(content);
            
            // Navigate the plist structure: plist -> dict -> key/value pairs
            var dictElement = doc.Root?.Element("dict");
            if (dictElement == null)
            {
                throw new InvalidOperationException($"Invalid Info.plist structure - missing dict element: {infoPlistPath}");
            }

            // Find CFBundleIdentifier key-value pair in the dictionary
            var keyElements = dictElement.Elements("key").ToList();
            for (int i = 0; i < keyElements.Count; i++)
            {
                var keyElement = keyElements[i];
                if (string.Equals(keyElement.Value, "CFBundleIdentifier", StringComparison.OrdinalIgnoreCase))
                {
                    // Get the next sibling element which should contain the value
                    var valueElement = keyElement.ElementsAfterSelf().FirstOrDefault();
                    if (valueElement?.Name == "string")
                    {
                        var bundleId = valueElement.Value?.Trim();
                        if (string.IsNullOrEmpty(bundleId))
                        {
                            throw new InvalidOperationException($"Empty CFBundleIdentifier value in Info.plist: {infoPlistPath}");
                        }
                        
                        return bundleId;
                    }
                    else
                    {
                        throw new InvalidOperationException($"CFBundleIdentifier value is not a string in Info.plist: {infoPlistPath}");
                    }
                }
            }

            throw new InvalidOperationException($"CFBundleIdentifier not found in Info.plist: {infoPlistPath}");
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidOperationException($"Invalid XML in Info.plist: {infoPlistPath}", ex);
        }
    }

    /// <summary>
    /// Extract the package name from an Android APK using aapt.
    /// </summary>
    /// <param name="apkPath">Path to the APK file</param>
    /// <returns>Package name</returns>
    public static string ExtractAndroidPackageName(string apkPath)
    {
        ValidateFilePath(apkPath, "Android APK");

        // This is a simplified approach - in production you might want to parse AndroidManifest.xml
        // or use a proper APK parsing library
        var fileName = Path.GetFileNameWithoutExtension(apkPath);
        
        // For now, assume package name can be derived from filename or is provided separately
        // This would need to be enhanced with actual aapt parsing
        return $"com.example.{fileName}".ToLowerInvariant();
    }
}