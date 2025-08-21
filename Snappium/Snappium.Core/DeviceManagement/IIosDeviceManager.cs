using Snappium.Core.Config;

namespace Snappium.Core.DeviceManagement;

/// <summary>
/// iOS-specific device manager interface for iOS simulators.
/// </summary>
public interface IIosDeviceManager : IDeviceManager
{
    /// <summary>
    /// Shutdown an iOS simulator.
    /// </summary>
    /// <param name="udidOrName">Device UDID or name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when simulator is shut down</returns>
    Task ShutdownAsync(string udidOrName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Boot an iOS simulator and wait until it's ready.
    /// </summary>
    /// <param name="udidOrName">Device UDID or name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when simulator is booted</returns>
    Task BootAsync(string udidOrName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set status bar overrides on an iOS simulator.
    /// </summary>
    /// <param name="udidOrName">Device UDID or name</param>
    /// <param name="statusBar">Status bar configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when status bar is configured</returns>
    Task SetStatusBarAsync(string udidOrName, IosStatusBar statusBar, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstall an app from an iOS simulator.
    /// </summary>
    /// <param name="deviceIdentifier">Device UDID or name</param>
    /// <param name="bundleId">iOS app bundle identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when app is uninstalled</returns>
    Task UninstallAppAsync(string deviceIdentifier, string bundleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Appium capabilities for an iOS device.
    /// </summary>
    /// <param name="device">iOS device configuration</param>
    /// <param name="languageTag">Language tag</param>
    /// <param name="localeMapping">Locale mapping</param>
    /// <param name="appPath">Path to the iOS app</param>
    /// <returns>Dictionary of Appium capabilities</returns>
    Dictionary<string, object> GetCapabilities(IosDevice device, string languageTag, LocaleMapping localeMapping, string appPath);
}