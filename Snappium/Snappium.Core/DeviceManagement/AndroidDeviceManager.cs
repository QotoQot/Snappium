using Microsoft.Extensions.Logging;
using Snappium.Core.Config;
using Snappium.Core.Infrastructure;
using System.Text.RegularExpressions;

namespace Snappium.Core.DeviceManagement;

/// <summary>
/// Android device manager for managing Android emulators using adb and emulator commands.
/// </summary>
public sealed class AndroidDeviceManager : IAndroidDeviceManager
{
    private readonly ICommandRunner _commandRunner;
    private readonly ILogger<AndroidDeviceManager> _logger;

    public AndroidDeviceManager(ICommandRunner commandRunner, ILogger<AndroidDeviceManager> logger)
    {
        _commandRunner = commandRunner;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> StartEmulatorAsync(string avdName, CancellationToken cancellationToken = default)
    {
        // Use default port range
        return await StartEmulatorAsync(avdName, Defaults.Ports.EmulatorStartPort, Defaults.Ports.EmulatorEndPort, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> StartEmulatorAsync(string avdName, int portRangeStart, int portRangeEnd, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Android emulator: {AvdName} (port range: {StartPort}-{EndPort})", avdName, portRangeStart, portRangeEnd);

        // Validate port range
        if (portRangeStart < 1024 || portRangeStart > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(portRangeStart), "Port range start must be between 1024 and 65535");
        }
        
        if (portRangeEnd < 1024 || portRangeEnd > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(portRangeEnd), "Port range end must be between 1024 and 65535");
        }
        
        if (portRangeStart >= portRangeEnd)
        {
            throw new ArgumentException("Port range start must be less than port range end");
        }

        // Find an available port for the emulator within the specified range
        var port = await FindAvailableEmulatorPortAsync(portRangeStart, portRangeEnd, cancellationToken);
        var emulatorSerial = $"emulator-{port}";

        var args = new List<string>
        {
            "-avd", avdName,
            "-port", port.ToString(),
            "-no-window",  // Run headless
            "-no-audio",   // Disable audio
            "-no-snapshot-save"  // Don't save snapshots
        };

        var emulatorCmd = GetEmulatorCommand();
        _logger.LogDebug("Starting emulator with command: {Command} {Args}", emulatorCmd, string.Join(" ", args));

        // Start the emulator in the background (don't wait for it to complete)
        var emulatorTask = _commandRunner.RunAsync(
            GetEmulatorCommand(),
            args.ToArray(),
            timeout: Defaults.Timeouts.BuildOperation,
            cancellationToken: cancellationToken);

        // Wait a bit for the emulator to start up
        await DeviceHelpers.DelayAsync(TimeSpan.FromSeconds(5), _logger, "emulator startup", cancellationToken);

        // Check if emulator is starting up
        var isStarting = await IsEmulatorStartingAsync(emulatorSerial, cancellationToken);
        if (!isStarting)
        {
            throw new InvalidOperationException($"Failed to start Android emulator {avdName}");
        }

        _logger.LogInformation("Android emulator {AvdName} is starting up on {Serial}", avdName, emulatorSerial);

        // Don't await the emulator task here - it runs until the emulator is shut down
        _ = emulatorTask.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                _logger.LogError(t.Exception, "Emulator process failed");
            }
        }, TaskScheduler.Default);

        return emulatorSerial;
    }

    /// <inheritdoc />
    public async Task WaitForBootAsync(string deviceSerial, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(5);
        _logger.LogInformation("Waiting for Android emulator {Serial} to finish booting", deviceSerial);

        var isBooted = await DeviceHelpers.PollUntilAsync(
            async () => await IsEmulatorBootedAsync(deviceSerial, cancellationToken),
            timeout: effectiveTimeout,
            pollingInterval: TimeSpan.FromSeconds(5),
            logger: _logger,
            operationName: $"emulator {deviceSerial} boot",
            cancellationToken: cancellationToken);

        if (!isBooted)
        {
            throw new TimeoutException($"Android emulator {deviceSerial} did not finish booting within {effectiveTimeout}");
        }

        _logger.LogInformation("Android emulator {Serial} is now booted and ready", deviceSerial);
    }

    /// <inheritdoc />
    public async Task SetLanguageAsync(string deviceSerial, string languageTag, LocaleMapping localeMapping, CancellationToken cancellationToken = default)
    {
        var androidLocale = localeMapping.Android;
        _logger.LogInformation("Setting Android emulator language to {Language} (locale: {Locale})", languageTag, androidLocale);

        // Set the system locale
        var localeResult = await RunAdbCommandAsync(
            deviceSerial,
            ["shell", "setprop", "persist.sys.locale", androidLocale],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!localeResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to set locale: {localeResult.StandardError}");
        }

        // Broadcast locale change
        var broadcastResult = await RunAdbCommandAsync(
            deviceSerial,
            ["shell", "am", "broadcast", "-a", "android.intent.action.LOCALE_CHANGED"],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!broadcastResult.IsSuccess)
        {
            _logger.LogWarning("Failed to broadcast locale change: {Error}", broadcastResult.StandardError);
        }

        _logger.LogDebug("Language configuration completed for {Serial}", deviceSerial);
    }

    /// <inheritdoc />
    public async Task SetStatusBarDemoModeAsync(string deviceSerial, AndroidStatusBar statusBar, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting Android status bar demo mode for {Serial}", deviceSerial);

        // Enable demo mode
        var enableResult = await RunAdbCommandAsync(
            deviceSerial,
            ["shell", "settings", "put", "global", "sysui_demo_allowed", "1"],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!enableResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to enable demo mode: {enableResult.StandardError}");
        }

        // Set status bar elements
        var commands = new List<string[]>();

        if (!string.IsNullOrEmpty(statusBar.Clock))
        {
            commands.Add(["shell", "am", "broadcast", "-a", "com.android.systemui.demo", "-e", "command", "clock", "-e", "hhmm", statusBar.Clock]);
        }

        if (statusBar.Battery.HasValue)
        {
            commands.Add(["shell", "am", "broadcast", "-a", "com.android.systemui.demo", "-e", "command", "battery", "-e", "level", statusBar.Battery.Value.ToString(), "-e", "plugged", "false"]);
        }

        if (!string.IsNullOrEmpty(statusBar.Wifi))
        {
            commands.Add(["shell", "am", "broadcast", "-a", "com.android.systemui.demo", "-e", "command", "network", "-e", "wifi", "show", "-e", "level", statusBar.Wifi]);
        }

        foreach (var command in commands)
        {
            var result = await RunAdbCommandAsync(deviceSerial, command, timeout: TimeSpan.FromMinutes(1), cancellationToken: cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to set status bar element: {Error}", result.StandardError);
            }
        }

        _logger.LogDebug("Status bar demo mode configured for {Serial}", deviceSerial);
    }

    /// <inheritdoc />
    public async Task InstallAppAsync(string deviceSerial, string apkPath, CancellationToken cancellationToken = default)
    {
        DeviceHelpers.ValidateFilePath(apkPath, "Android APK");
        _logger.LogInformation("Installing Android app {ApkPath} on {Serial}", apkPath, deviceSerial);

        var result = await RunAdbCommandAsync(
            deviceSerial,
            ["install", "-r", apkPath],  // -r flag allows reinstallation
            timeout: TimeSpan.FromMinutes(5),
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to install app {apkPath}: {result.StandardError}");
        }

        _logger.LogDebug("App installation completed for {ApkPath}", apkPath);
    }

    /// <inheritdoc />
    public async Task TakeScreenshotAsync(string deviceSerial, string outputFilePath, CancellationToken cancellationToken = default)
    {
        DeviceHelpers.EnsureDirectoryExists(Path.GetDirectoryName(outputFilePath)!);
        _logger.LogDebug("Taking Android screenshot: {OutputPath}", outputFilePath);

        // Take screenshot on device
        var tempPath = "/sdcard/screenshot.png";
        var screenshotResult = await RunAdbCommandAsync(
            deviceSerial,
            ["shell", "screencap", "-p", tempPath],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!screenshotResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to take screenshot: {screenshotResult.StandardError}");
        }

        // Pull screenshot to host
        var pullResult = await RunAdbCommandAsync(
            deviceSerial,
            ["pull", tempPath, outputFilePath],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!pullResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to pull screenshot: {pullResult.StandardError}");
        }

        // Clean up temp file
        await RunAdbCommandAsync(
            deviceSerial,
            ["shell", "rm", tempPath],
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        // Verify the screenshot was created
        if (!File.Exists(outputFilePath))
        {
            throw new InvalidOperationException($"Screenshot was not created at expected path: {outputFilePath}");
        }

        _logger.LogDebug("Screenshot saved: {OutputPath}", outputFilePath);
    }

    /// <inheritdoc />
    public async Task ResetAppDataAsync(string deviceSerial, string packageName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting app data for {PackageName} on {Serial}", packageName, deviceSerial);

        var result = await RunAdbCommandAsync(
            deviceSerial,
            ["shell", "pm", "clear", packageName],
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            // App reset can fail if app isn't installed - log warning but don't throw
            _logger.LogWarning("App data reset failed for {PackageName}: {Error}", packageName, result.StandardError);
        }
        else
        {
            _logger.LogDebug("App data reset completed for {PackageName}", packageName);
        }
    }

    /// <inheritdoc />
    public async Task StopEmulatorAsync(string deviceSerial, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping Android emulator {Serial}", deviceSerial);

        var result = await RunAdbCommandAsync(
            deviceSerial,
            ["emu", "kill"],
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to stop emulator gracefully: {Error}", result.StandardError);
        }

        _logger.LogDebug("Emulator stop command completed for {Serial}", deviceSerial);
    }

    /// <inheritdoc />
    public Dictionary<string, object> GetCapabilities(AndroidDevice device, string languageTag, LocaleMapping localeMapping, string apkPath)
    {
        return new Dictionary<string, object>
        {
            ["platformName"] = "Android",
            ["platformVersion"] = device.PlatformVersion,
            ["deviceName"] = device.Name,
            ["avd"] = device.Avd,
            ["app"] = apkPath,
            ["language"] = localeMapping.Android,
            ["locale"] = localeMapping.Android,
            ["autoGrantPermissions"] = true,
            ["noReset"] = true,
            ["newCommandTimeout"] = 300,
            ["adbExecTimeout"] = 60000,
            ["androidInstallTimeout"] = 300000
        };
    }

    private async Task<Infrastructure.CommandResult> RunAdbCommandAsync(
        string deviceSerial,
        string[] args,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var fullArgs = new List<string> { "-s", deviceSerial };
        fullArgs.AddRange(args);

        return await _commandRunner.RunAsync(
            "adb",
            fullArgs.ToArray(),
            timeout: timeout,
            cancellationToken: cancellationToken);
    }

    private async Task<int> FindAvailableEmulatorPortAsync(int portRangeStart, int portRangeEnd, CancellationToken cancellationToken)
    {
        // Find an available port for the emulator within the specified range
        // Emulator uses even ports, so start from the first even port in range
        int startPort = portRangeStart % 2 == 0 ? portRangeStart : portRangeStart + 1;
        
        for (int port = startPort; port < portRangeEnd; port += 2)
        {
            var result = await _commandRunner.RunAsync(
                "adb",
                ["devices"],
                timeout: Defaults.Timeouts.ShortOperation,
                cancellationToken: cancellationToken);

            if (result.IsSuccess && !result.StandardOutput.Contains($"emulator-{port}"))
            {
                _logger.LogDebug("Found available emulator port: {Port}", port);
                return port;
            }
        }

        throw new InvalidOperationException($"No available emulator ports found in range {portRangeStart}-{portRangeEnd}");
    }

    private async Task<bool> IsEmulatorStartingAsync(string emulatorSerial, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync(
                "adb",
                ["devices"],
                timeout: Defaults.Timeouts.ShortOperation,
                cancellationToken: cancellationToken);

            return result.IsSuccess && result.StandardOutput.Contains(emulatorSerial);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while checking emulator startup status");
            return false;
        }
    }

    private async Task<bool> IsEmulatorBootedAsync(string deviceSerial, CancellationToken cancellationToken)
    {
        try
        {
            var result = await RunAdbCommandAsync(
                deviceSerial,
                ["shell", "getprop", "sys.boot_completed"],
                timeout: Defaults.Timeouts.ShortOperation,
                cancellationToken: cancellationToken);

            return result.IsSuccess && result.StandardOutput.Trim() == "1";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while checking emulator boot status");
            return false;
        }
    }

    private static string GetEmulatorCommand()
    {
        // Get Android SDK path from ANDROID_HOME environment variable
        var androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (string.IsNullOrEmpty(androidHome))
        {
            throw new InvalidOperationException("ANDROID_HOME environment variable is not set");
        }

        // Build full path to emulator executable
        if (Os.IsWindows)
        {
            return Path.Combine(androidHome, "emulator", "emulator.exe");
        }
        else
        {
            return Path.Combine(androidHome, "emulator", "emulator");
        }
    }

    /// <inheritdoc />
    public async Task<string> CaptureLogsAsync(string deviceIdentifier, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Capturing Android device logs for device: {Device}", deviceIdentifier);

        try
        {
            // Use adb logcat to capture recent logs with filtering for important entries
            var result = await _commandRunner.RunAsync(
                "adb",
                ["-s", deviceIdentifier, "logcat", "-d", "-v", "time", "*:W", "System.err:V"],
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            if (result.IsSuccess)
            {
                var logs = result.StandardOutput;
                _logger.LogDebug("Successfully captured {LogLength} characters of Android logs", logs.Length);
                
                // Limit log size to prevent excessive memory usage (last 50KB)
                if (logs.Length > 50000)
                {
                    logs = "... (truncated) ...\n" + logs.Substring(logs.Length - 50000);
                }
                
                return logs;
            }
            else
            {
                _logger.LogWarning("Failed to capture Android logs (exit code {ExitCode}): {Error}", 
                    result.ExitCode, result.StandardError);
                return $"Failed to capture Android logs: {result.StandardError}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while capturing Android logs for device: {Device}", deviceIdentifier);
            return $"Exception while capturing Android logs: {ex.Message}";
        }
    }
}