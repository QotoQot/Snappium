using Microsoft.Extensions.Logging;
using Snappium.Core.Config;
using Snappium.Core.Infrastructure;
using System.Text.Json;

namespace Snappium.Core.DeviceManagement;

/// <summary>
/// iOS device manager for managing iOS simulators using xcrun simctl.
/// </summary>
public sealed class IosDeviceManager : IIosDeviceManager
{
    readonly ICommandRunner _commandRunner;
    readonly ILogger<IosDeviceManager> _logger;

    public IosDeviceManager(ICommandRunner commandRunner, ILogger<IosDeviceManager> logger)
    {
        _commandRunner = commandRunner;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ShutdownAsync(string udidOrName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Shutting down iOS simulator: {Device}", udidOrName);

        var result = await _commandRunner.RunAsync(
            "xcrun",
            ["simctl", "shutdown", udidOrName],
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            // Shutdown can fail if device is already shut down - this is often acceptable
            _logger.LogWarning("Simulator shutdown command failed (exit code {ExitCode}): {Error}", 
                result.ExitCode, result.StandardError);
        }
        else
        {
            _logger.LogDebug("Simulator shutdown completed: {Output}", result.StandardOutput);
        }
        
        // Wait for the simulator to fully shut down
        _logger.LogDebug("Waiting for simulator {Device} to fully shut down", udidOrName);
        var shutdownComplete = await DeviceHelpers.PollUntilAsync(
            async () =>
            {
                var listResult = await _commandRunner.RunAsync(
                    "xcrun",
                    ["simctl", "list", "devices", "-j"],
                    timeout: TimeSpan.FromSeconds(5),
                    cancellationToken: cancellationToken);
                
                if (!listResult.IsSuccess) return false;
                
                try
                {
                    var json = JsonDocument.Parse(listResult.StandardOutput);
                    var devices = json.RootElement.GetProperty("devices");
                    
                    foreach (var runtimeDevices in devices.EnumerateObject())
                    {
                        foreach (var device in runtimeDevices.Value.EnumerateArray())
                        {
                            var deviceUdid = device.GetProperty("udid").GetString();
                            var deviceName = device.GetProperty("name").GetString();
                            var state = device.GetProperty("state").GetString();
                            
                            if ((deviceUdid == udidOrName || deviceName == udidOrName) && state == "Shutdown")
                            {
                                return true;
                            }
                        }
                    }
                }
                catch
                {
                    // If parsing fails, assume not shut down yet
                    return false;
                }
                
                return false;
            },
            timeout: TimeSpan.FromSeconds(30),
            pollingInterval: TimeSpan.FromSeconds(1),
            logger: _logger,
            operationName: $"simulator {udidOrName} shutdown",
            cancellationToken: cancellationToken);

        if (!shutdownComplete)
        {
            _logger.LogWarning("Simulator {Device} did not fully shut down within timeout", udidOrName);
        }
        else
        {
            _logger.LogDebug("Simulator {Device} has fully shut down", udidOrName);
        }
        
        // Add a small delay to ensure all resources are released
        await DeviceHelpers.DelayAsync(TimeSpan.FromSeconds(2), _logger, "post-shutdown cleanup", cancellationToken);
    }

    /// <inheritdoc />
    public async Task BootAsync(string udidOrName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Booting iOS simulator: {Device}", udidOrName);

        // First boot the simulator
        var bootResult = await _commandRunner.RunAsync(
            "xcrun",
            ["simctl", "boot", udidOrName],
            timeout: TimeSpan.FromMinutes(3),
            cancellationToken: cancellationToken);

        if (!bootResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to boot simulator {udidOrName}: {bootResult.StandardError}");
        }

        _logger.LogDebug("Boot command completed, waiting for simulator to be ready");

        // Wait for the simulator to reach "Booted" state
        var isReady = await DeviceHelpers.PollUntilAsync(
            async () => await IsSimulatorBootedAsync(udidOrName, cancellationToken),
            timeout: TimeSpan.FromMinutes(3),
            pollingInterval: TimeSpan.FromSeconds(3),
            logger: _logger,
            operationName: $"simulator {udidOrName} boot",
            cancellationToken: cancellationToken);

        if (!isReady)
        {
            throw new TimeoutException($"Simulator {udidOrName} did not reach booted state within timeout");
        }

        _logger.LogInformation("iOS simulator {Device} is now booted and ready", udidOrName);
    }

    /// <inheritdoc />
    public async Task SetLanguageAsync(string deviceIdentifier, string languageTag, LocaleMapping localeMapping, CancellationToken cancellationToken = default)
    {
        var iosLocale = localeMapping.Ios;
        _logger.LogInformation("Setting iOS simulator language to {Language} (locale: {Locale})", languageTag, iosLocale);

        // Set AppleLanguages
        var languageResult = await _commandRunner.RunAsync(
            "xcrun",
            ["simctl", "spawn", deviceIdentifier, "defaults", "write", "-g", "AppleLanguages", "-array", iosLocale],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!languageResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to set AppleLanguages: {languageResult.StandardError}");
        }

        // Set AppleLocale
        var localeResult = await _commandRunner.RunAsync(
            "xcrun",
            ["simctl", "spawn", deviceIdentifier, "defaults", "write", "-g", "AppleLocale", iosLocale],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!localeResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to set AppleLocale: {localeResult.StandardError}");
        }

        // Force light mode to ensure consistent appearance
        var interfaceStyleResult = await _commandRunner.RunAsync(
            "xcrun",
            ["simctl", "spawn", deviceIdentifier, "defaults", "write", "-g", "AppleInterfaceStyle", "Light"],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!interfaceStyleResult.IsSuccess)
        {
            _logger.LogWarning("Failed to set light mode (continuing anyway): {Error}", interfaceStyleResult.StandardError);
        }
        else
        {
            _logger.LogDebug("Light mode enforced for {Device}", deviceIdentifier);
        }

        _logger.LogDebug("Language configuration completed for {Device}", deviceIdentifier);
    }

    /// <inheritdoc />
    public async Task SetStatusBarAsync(string udidOrName, IosStatusBar statusBar, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting iOS status bar overrides for {Device}", udidOrName);

        var args = new List<string> { "simctl", "status_bar", udidOrName, "override" };

        // Add status bar configuration arguments
        if (!string.IsNullOrEmpty(statusBar.Time))
        {
            args.AddRange(["--time", statusBar.Time]);
        }

        if (statusBar.WifiBars.HasValue)
        {
            args.AddRange(["--wifiBars", statusBar.WifiBars.Value.ToString()]);
        }

        if (statusBar.CellularBars.HasValue)
        {
            args.AddRange(["--cellularBars", statusBar.CellularBars.Value.ToString()]);
        }

        if (!string.IsNullOrEmpty(statusBar.BatteryState))
        {
            args.AddRange(["--batteryState", statusBar.BatteryState]);
        }

        var result = await _commandRunner.RunAsync(
            "xcrun",
            args.ToArray(),
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to set status bar overrides: {result.StandardError}");
        }

        _logger.LogDebug("Status bar overrides applied for {Device}", udidOrName);
    }

    /// <inheritdoc />
    public async Task InstallAppAsync(string deviceIdentifier, string appPath, CancellationToken cancellationToken = default)
    {
        DeviceHelpers.ValidateFilePath(appPath, "iOS app");
        _logger.LogInformation("Installing iOS app {AppPath} on {Device}", appPath, deviceIdentifier);

        // Note: Fresh installs would require uninstalling first, but without bundle ID configuration
        // we cannot determine what to uninstall. Users should manually clean apps if needed.

        var result = await _commandRunner.RunAsync(
            "xcrun",
            ["simctl", "install", deviceIdentifier, appPath],
            timeout: TimeSpan.FromMinutes(5),
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to install app {appPath}: {result.StandardError}");
        }

        _logger.LogDebug("App installation completed for {AppPath}", appPath);
    }

    /// <summary>
    /// Uninstalls an iOS app from the simulator.
    /// </summary>
    /// <param name="deviceIdentifier">Device UDID or name</param>
    /// <param name="bundleId">Bundle identifier of the app to uninstall</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task UninstallAppAsync(string deviceIdentifier, string bundleId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Uninstalling app {BundleId} from {Device}", bundleId, deviceIdentifier);
            
            var result = await _commandRunner.RunAsync(
                "xcrun",
                ["simctl", "uninstall", deviceIdentifier, bundleId],
                timeout: TimeSpan.FromMinutes(1),
                cancellationToken: cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogDebug("Successfully uninstalled app {BundleId} from {Device}", bundleId, deviceIdentifier);
            }
            else
            {
                // App might not be installed - log but don't throw
                _logger.LogDebug("App {BundleId} was not installed or uninstall failed: {Error}", bundleId, result.StandardError);
            }
        }
        catch (Exception ex)
        {
            // Uninstall failures are not critical - log but continue
            _logger.LogDebug(ex, "App uninstall failed for {BundleId}, continuing with installation", bundleId);
        }
    }

    /// <inheritdoc />
    public async Task TakeScreenshotAsync(string deviceIdentifier, string outputFilePath, CancellationToken cancellationToken = default)
    {
        DeviceHelpers.EnsureDirectoryExists(Path.GetDirectoryName(outputFilePath)!);
        _logger.LogDebug("Taking iOS screenshot: {OutputPath}", outputFilePath);

        var result = await _commandRunner.RunAsync(
            "xcrun",
            ["simctl", "io", deviceIdentifier, "screenshot", outputFilePath],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to take screenshot: {result.StandardError}");
        }

        // Verify the screenshot was created
        if (!File.Exists(outputFilePath))
        {
            throw new InvalidOperationException($"Screenshot was not created at expected path: {outputFilePath}");
        }

        _logger.LogDebug("Screenshot saved: {OutputPath}", outputFilePath);
    }

    /// <inheritdoc />
    public async Task ResetAppDataAsync(string deviceIdentifier, string bundleId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting app data for {BundleId} on {Device}", bundleId, deviceIdentifier);

        var result = await _commandRunner.RunAsync(
            "xcrun",
            ["simctl", "privacy", deviceIdentifier, "reset", "all", bundleId],
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            // App reset can fail if app isn't installed - log warning but don't throw
            _logger.LogWarning("App data reset failed for {BundleId}: {Error}", bundleId, result.StandardError);
        }
        else
        {
            _logger.LogDebug("App data reset completed for {BundleId}", bundleId);
        }
    }

    /// <inheritdoc />
    public Dictionary<string, object> GetCapabilities(IosDevice device, string languageTag, LocaleMapping localeMapping, string appPath)
    {
        var deviceId = DeviceHelpers.GetDeviceIdentifier(device.Udid, device.Name);
        
        return new Dictionary<string, object>
        {
            ["platformName"] = "iOS",
            ["platformVersion"] = device.PlatformVersion,
            ["deviceName"] = device.Name,
            ["automationName"] = "XCUITest",
            ["udid"] = deviceId,
            ["app"] = appPath,
            ["language"] = localeMapping.Ios,
            ["locale"] = localeMapping.Ios,
            ["autoAcceptAlerts"] = true,
            ["autoDismissAlerts"] = true,
            ["noReset"] = true,
            ["newCommandTimeout"] = 300,
            ["wdaStartupRetries"] = 3,
            ["wdaStartupRetryInterval"] = 5000,
            ["shouldUseSingletonTestManager"] = false
        };
    }

    async Task<bool> IsSimulatorBootedAsync(string udidOrName, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync(
                "xcrun",
                ["simctl", "list", "devices", "-j"],
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to check simulator status: {Error}", result.StandardError);
                return false;
            }

            var json = JsonDocument.Parse(result.StandardOutput);
            var devices = json.RootElement.GetProperty("devices");

            foreach (var osProperty in devices.EnumerateObject())
            {
                foreach (var device in osProperty.Value.EnumerateArray())
                {
                    var name = device.GetProperty("name").GetString();
                    var udid = device.GetProperty("udid").GetString();
                    var state = device.GetProperty("state").GetString();

                    if ((name == udidOrName || udid == udidOrName) && state == "Booted")
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while checking simulator boot status");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<string> CaptureLogsAsync(string deviceIdentifier, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Capturing iOS simulator logs for device: {Device}", deviceIdentifier);

        try
        {
            // Use simctl log show to capture recent logs (last 5 minutes)
            var result = await _commandRunner.RunAsync(
                "xcrun",
                ["simctl", "log", "show", deviceIdentifier, "--style", "compact", "--start", "2024-01-01", "--predicate", "subsystem CONTAINS 'com.apple' OR category = 'default'"],
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            if (result.IsSuccess)
            {
                var logs = result.StandardOutput;
                _logger.LogDebug("Successfully captured {LogLength} characters of iOS logs", logs.Length);
                
                // Limit log size to prevent excessive memory usage (last 50KB)
                if (logs.Length > 50000)
                {
                    logs = "... (truncated) ...\n" + logs.Substring(logs.Length - 50000);
                }
                
                return logs;
            }

            _logger.LogWarning("Failed to capture iOS logs (exit code {ExitCode}): {Error}", 
                result.ExitCode, result.StandardError);
            return $"Failed to capture iOS logs: {result.StandardError}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while capturing iOS logs for device: {Device}", deviceIdentifier);
            return $"Exception while capturing iOS logs: {ex.Message}";
        }
    }
}