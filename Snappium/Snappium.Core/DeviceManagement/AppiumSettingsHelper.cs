using Microsoft.Extensions.Logging;
using Snappium.Core.Infrastructure;
using System.Reflection;

namespace Snappium.Core.DeviceManagement;

/// <summary>
/// Helper class for managing the Appium Settings app on Android devices.
/// This app provides reliable system setting changes on modern Android versions.
/// </summary>
public sealed class AppiumSettingsHelper
{
    const string AppiumSettingsPackage = "io.appium.settings";
    const string ApkResourceName = "Snappium.Core.Resources.Android.settings_apk-debug.apk";

    readonly ICommandRunner _commandRunner;
    readonly ILogger<AppiumSettingsHelper> _logger;

    public AppiumSettingsHelper(ICommandRunner commandRunner, ILogger<AppiumSettingsHelper> logger)
    {
        _commandRunner = commandRunner;
        _logger = logger;
    }

    /// <summary>
    /// Ensures Appium Settings app is installed and has necessary permissions.
    /// </summary>
    public async Task EnsureInstalledAsync(string deviceSerial, CancellationToken cancellationToken = default)
    {
        // Check if app is already installed
        var isInstalled = await IsAppInstalledAsync(deviceSerial, cancellationToken);
        if (!isInstalled)
        {
            await InstallAppAsync(deviceSerial, cancellationToken);
        }

        // Grant necessary permissions
        await GrantPermissionsAsync(deviceSerial, cancellationToken);
    }

    /// <summary>
    /// Changes the system locale using Appium Settings broadcast.
    /// </summary>
    public async Task SetLocaleAsync(string deviceSerial, string language, string country, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Setting locale to {Language}-{Country} using Appium Settings", language, country);

        var result = await _commandRunner.RunAsync(
            "adb",
            ["-s", deviceSerial, "shell", "am", "broadcast",
             "-a", "io.appium.settings.locale",
             "-n", "io.appium.settings/.receivers.LocaleSettingReceiver",
             "--es", "lang", language,
             "--es", "country", country],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogDebug("Successfully set locale to {Language}-{Country}", language, country);
        }
        else
        {
            throw new InvalidOperationException($"Failed to set locale via Appium Settings: {result.StandardError}");
        }
    }

    /// <summary>
    /// Checks if Appium Settings app is installed on the device.
    /// </summary>
    async Task<bool> IsAppInstalledAsync(string deviceSerial, CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync(
            "adb",
            ["-s", deviceSerial, "shell", "pm", "list", "packages", AppiumSettingsPackage],
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        return result.IsSuccess && result.StandardOutput.Contains(AppiumSettingsPackage);
    }

    /// <summary>
    /// Installs the Appium Settings APK from embedded resources.
    /// </summary>
    async Task InstallAppAsync(string deviceSerial, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Installing Appium Settings app on device {Serial}", deviceSerial);

        // Extract APK to temporary file
        var tempApkPath = await ExtractApkToTempFileAsync(cancellationToken);
        
        try
        {
            var result = await _commandRunner.RunAsync(
                "adb",
                ["-s", deviceSerial, "install", "-g", tempApkPath], // -g grants permissions automatically
                timeout: TimeSpan.FromMinutes(2),
                cancellationToken: cancellationToken);

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Failed to install Appium Settings: {result.StandardError}");
            }

            _logger.LogDebug("Successfully installed Appium Settings app");
        }
        finally
        {
            // Clean up temporary file
            try
            {
                File.Delete(tempApkPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary APK file: {Path}", tempApkPath);
            }
        }
    }

    /// <summary>
    /// Grants necessary permissions to Appium Settings app.
    /// </summary>
    async Task GrantPermissionsAsync(string deviceSerial, CancellationToken cancellationToken)
    {
        var permissions = new[]
        {
            "android.permission.CHANGE_CONFIGURATION",
            "android.permission.ACCESS_FINE_LOCATION",
            "android.permission.RECORD_AUDIO"
        };

        foreach (var permission in permissions)
        {
            var result = await _commandRunner.RunAsync(
                "adb",
                ["-s", deviceSerial, "shell", "pm", "grant", AppiumSettingsPackage, permission],
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogDebug("Granted permission {Permission} to Appium Settings", permission);
            }
            else
            {
                _logger.LogWarning("Failed to grant permission {Permission}: {Error}", permission, result.StandardError);
            }
        }

        // Grant mock location permission using appops
        var mockLocationResult = await _commandRunner.RunAsync(
            "adb",
            ["-s", deviceSerial, "shell", "appops", "set", AppiumSettingsPackage, "android:mock_location", "allow"],
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        if (mockLocationResult.IsSuccess)
        {
            _logger.LogDebug("Granted mock location permission to Appium Settings");
        }
        else
        {
            _logger.LogWarning("Failed to grant mock location permission: {Error}", mockLocationResult.StandardError);
        }
    }

    /// <summary>
    /// Extracts the embedded APK resource to a temporary file.
    /// </summary>
    async Task<string> ExtractApkToTempFileAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var tempPath = Path.GetTempFileName();
        var apkPath = Path.ChangeExtension(tempPath, ".apk");
        
        // Rename to .apk extension
        File.Move(tempPath, apkPath);

        await using var resourceStream = assembly.GetManifestResourceStream(ApkResourceName);
        if (resourceStream == null)
        {
            throw new InvalidOperationException($"Could not find embedded resource: {ApkResourceName}");
        }

        await using var fileStream = File.Create(apkPath);
        await resourceStream.CopyToAsync(fileStream, cancellationToken);

        _logger.LogDebug("Extracted Appium Settings APK to: {Path}", apkPath);
        return apkPath;
    }
}