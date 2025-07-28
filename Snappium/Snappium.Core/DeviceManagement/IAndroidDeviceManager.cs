using Snappium.Core.Config;

namespace Snappium.Core.DeviceManagement;

/// <summary>
/// Android-specific device manager interface for Android emulators.
/// </summary>
public interface IAndroidDeviceManager : IDeviceManager
{
    /// <summary>
    /// Start an Android emulator.
    /// </summary>
    /// <param name="avdName">AVD name to start</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that returns the emulator serial when started</returns>
    Task<string> StartEmulatorAsync(string avdName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Wait for the Android emulator to finish booting.
    /// </summary>
    /// <param name="deviceSerial">Emulator serial number</param>
    /// <param name="timeout">Maximum time to wait for boot</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when emulator is ready</returns>
    Task WaitForBootAsync(string deviceSerial, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set status bar demo mode on Android emulator.
    /// </summary>
    /// <param name="deviceSerial">Emulator serial number</param>
    /// <param name="statusBar">Status bar configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when status bar is configured</returns>
    Task SetStatusBarDemoModeAsync(string deviceSerial, AndroidStatusBar statusBar, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the Android emulator.
    /// </summary>
    /// <param name="deviceSerial">Emulator serial number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when emulator is stopped</returns>
    Task StopEmulatorAsync(string deviceSerial, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Appium capabilities for an Android device.
    /// </summary>
    /// <param name="device">Android device configuration</param>
    /// <param name="languageTag">Language tag</param>
    /// <param name="localeMapping">Locale mapping</param>
    /// <param name="apkPath">Path to the Android APK</param>
    /// <returns>Dictionary of Appium capabilities</returns>
    Dictionary<string, object> GetCapabilities(AndroidDevice device, string languageTag, LocaleMapping localeMapping, string apkPath);
}