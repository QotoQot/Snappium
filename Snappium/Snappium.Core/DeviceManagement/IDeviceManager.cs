using Snappium.Core.Config;

namespace Snappium.Core.DeviceManagement;

/// <summary>
/// Base interface for device managers that handle platform-specific device operations.
/// </summary>
public interface IDeviceManager
{
    /// <summary>
    /// Take a screenshot on the device and save it to the specified path.
    /// </summary>
    /// <param name="deviceIdentifier">Device identifier (UDID for iOS, serial for Android)</param>
    /// <param name="outputFilePath">Path where the screenshot should be saved</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when screenshot is captured</returns>
    Task TakeScreenshotAsync(string deviceIdentifier, string outputFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Install an application on the device.
    /// </summary>
    /// <param name="deviceIdentifier">Device identifier (UDID for iOS, serial for Android)</param>
    /// <param name="appPath">Path to the application file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when app is installed</returns>
    Task InstallAppAsync(string deviceIdentifier, string appPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset application data for the specified app.
    /// </summary>
    /// <param name="deviceIdentifier">Device identifier (UDID for iOS, serial for Android)</param>
    /// <param name="appIdentifier">Application identifier (bundle ID for iOS, package name for Android)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when app data is reset</returns>
    Task ResetAppDataAsync(string deviceIdentifier, string appIdentifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set the device language and locale.
    /// </summary>
    /// <param name="deviceIdentifier">Device identifier (UDID for iOS, serial for Android)</param>
    /// <param name="languageTag">Language tag (e.g., "en-US")</param>
    /// <param name="localeMapping">Platform-specific locale mapping</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when language is set</returns>
    Task SetLanguageAsync(string deviceIdentifier, string languageTag, LocaleMapping localeMapping, CancellationToken cancellationToken = default);

    /// <summary>
    /// Capture device logs for diagnostic purposes.
    /// </summary>
    /// <param name="deviceIdentifier">Device identifier (UDID for iOS, serial for Android)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that returns the captured device logs as a string</returns>
    Task<string> CaptureLogsAsync(string deviceIdentifier, CancellationToken cancellationToken = default);
}