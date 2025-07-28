namespace Snappium.Core.Config;

/// <summary>
/// Centralized default values and constants used throughout the Snappium system.
/// This provides a single source of truth for all default settings.
/// </summary>
public static class Defaults
{
    /// <summary>
    /// Default port configuration values.
    /// </summary>
    public static class Ports
    {
        /// <summary>
        /// Default base port for Appium server allocation.
        /// </summary>
        public const int AppiumBasePort = 4723;

        /// <summary>
        /// Default port offset between parallel jobs to prevent conflicts.
        /// </summary>
        public const int PortOffset = 10;

        /// <summary>
        /// Default starting port for Android emulator allocation.
        /// </summary>
        public const int EmulatorStartPort = 5554;

        /// <summary>
        /// Default ending port for Android emulator allocation.
        /// </summary>
        public const int EmulatorEndPort = 5600;

        /// <summary>
        /// Minimum valid port number.
        /// </summary>
        public const int MinPortNumber = 1024;

        /// <summary>
        /// Maximum valid port number.
        /// </summary>
        public const int MaxPortNumber = 65535;

        /// <summary>
        /// Maximum allowed port offset to prevent excessive port ranges.
        /// </summary>
        public const int MaxPortOffset = 100;
    }

    /// <summary>
    /// Default timeout values for various operations.
    /// </summary>
    public static class Timeouts
    {
        /// <summary>
        /// Default timeout for device log capture operations.
        /// </summary>
        public static readonly TimeSpan DeviceLogCapture = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Default timeout for device operations like boot, shutdown, app install.
        /// </summary>
        public static readonly TimeSpan DeviceOperation = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Default polling interval for device status checks.
        /// </summary>
        public static readonly TimeSpan DevicePolling = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Default timeout for short operations like command execution and HTTP requests.
        /// </summary>
        public static readonly TimeSpan ShortOperation = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Default timeout for build operations.
        /// </summary>
        public static readonly TimeSpan BuildOperation = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Default timeout for element finding and interaction operations.
        /// </summary>
        public static readonly TimeSpan ElementOperation = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Default delay for retry operations.
        /// </summary>
        public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(10);
    }

    /// <summary>
    /// Default concurrency and performance settings.
    /// </summary>
    public static class Concurrency
    {
        /// <summary>
        /// Default concurrency calculation: use half of available processors for resource-intensive operations.
        /// </summary>
        /// <param name="jobCount">Number of jobs to execute</param>
        /// <returns>Optimal concurrency level</returns>
        public static int CalculateOptimalConcurrency(int jobCount)
        {
            if (jobCount <= 1)
                return 1;

            // Base concurrency on logical processor count, but cap it for resource management
            var processorCount = Environment.ProcessorCount;
            
            // For screenshot automation, each job uses significant resources (emulators, drivers, etc.)
            // So we use a more conservative concurrency level than CPU-bound tasks
            var maxConcurrency = Math.Max(1, processorCount / 2);
            
            // Don't exceed the number of jobs we actually have
            return Math.Min(maxConcurrency, jobCount);
        }
    }

    /// <summary>
    /// Default limits and constraints.
    /// </summary>
    public static class Limits
    {
        /// <summary>
        /// Maximum device log size to prevent excessive memory usage (50KB).
        /// </summary>
        public const int MaxDeviceLogSize = 50000;

        /// <summary>
        /// Truncation message for logs that exceed the maximum size.
        /// </summary>
        public const string LogTruncationMessage = "... (truncated) ...\n";
    }
}