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

    private string? _currentAvd;
    private string? _emulatorSerial;

    public AndroidDeviceManager(ICommandRunner commandRunner, ILogger<AndroidDeviceManager> logger)
    {
        _commandRunner = commandRunner;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartEmulatorAsync(string avdName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Android emulator: {AvdName}", avdName);
        _currentAvd = avdName;

        // Find an available port for the emulator
        var port = await FindAvailableEmulatorPortAsync(cancellationToken);
        _emulatorSerial = $"emulator-{port}";

        var args = new List<string>
        {
            "-avd", avdName,
            "-port", port.ToString(),
            "-no-window",  // Run headless
            "-no-audio",   // Disable audio
            "-no-snapshot-save",  // Don't save snapshots
            "-wipe-data"   // Start with clean state
        };

        _logger.LogDebug("Starting emulator with command: emulator {Args}", string.Join(" ", args));

        // Start the emulator in the background (don't wait for it to complete)
        var emulatorTask = _commandRunner.RunAsync(
            GetEmulatorCommand(),
            args.ToArray(),
            timeout: TimeSpan.FromMinutes(10),
            cancellationToken: cancellationToken);

        // Wait a bit for the emulator to start up
        await DeviceHelpers.DelayAsync(TimeSpan.FromSeconds(5), _logger, "emulator startup", cancellationToken);

        // Check if emulator is starting up
        var isStarting = await IsEmulatorStartingAsync(cancellationToken);
        if (!isStarting)
        {
            throw new InvalidOperationException($"Failed to start Android emulator {avdName}");
        }

        _logger.LogInformation("Android emulator {AvdName} is starting up on {Serial}", avdName, _emulatorSerial);

        // Don't await the emulator task here - it runs until the emulator is shut down
        _ = emulatorTask.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                _logger.LogError(t.Exception, "Emulator process failed");
            }
        }, TaskScheduler.Default);
    }

    /// <inheritdoc />
    public async Task WaitForBootAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_emulatorSerial == null)
        {
            throw new InvalidOperationException("No emulator is currently starting. Start an emulator first.");
        }

        var effectiveTimeout = timeout ?? TimeSpan.FromMinutes(5);
        _logger.LogInformation("Waiting for Android emulator {Serial} to finish booting", _emulatorSerial);

        var isBooted = await DeviceHelpers.PollUntilAsync(
            async () => await IsEmulatorBootedAsync(cancellationToken),
            timeout: effectiveTimeout,
            pollingInterval: TimeSpan.FromSeconds(5),
            logger: _logger,
            operationName: $"emulator {_emulatorSerial} boot",
            cancellationToken: cancellationToken);

        if (!isBooted)
        {
            throw new TimeoutException($"Android emulator {_emulatorSerial} did not finish booting within {effectiveTimeout}");
        }

        _logger.LogInformation("Android emulator {Serial} is now booted and ready", _emulatorSerial);
    }

    /// <inheritdoc />
    public async Task SetLanguageAsync(string languageTag, LocaleMapping localeMapping, CancellationToken cancellationToken = default)
    {
        if (_emulatorSerial == null)
        {
            throw new InvalidOperationException("No emulator is currently active. Start an emulator first.");
        }

        var androidLocale = localeMapping.Android;
        _logger.LogInformation("Setting Android emulator language to {Language} (locale: {Locale})", languageTag, androidLocale);

        // Set the system locale
        var localeResult = await RunAdbCommandAsync(
            ["shell", "setprop", "persist.sys.locale", androidLocale],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!localeResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to set locale: {localeResult.StandardError}");
        }

        // Broadcast locale change
        var broadcastResult = await RunAdbCommandAsync(
            ["shell", "am", "broadcast", "-a", "android.intent.action.LOCALE_CHANGED"],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!broadcastResult.IsSuccess)
        {
            _logger.LogWarning("Failed to broadcast locale change: {Error}", broadcastResult.StandardError);
        }

        _logger.LogDebug("Language configuration completed for {Serial}", _emulatorSerial);
    }

    /// <inheritdoc />
    public async Task SetStatusBarDemoModeAsync(AndroidStatusBar statusBar, CancellationToken cancellationToken = default)
    {
        if (_emulatorSerial == null)
        {
            throw new InvalidOperationException("No emulator is currently active. Start an emulator first.");
        }

        _logger.LogInformation("Setting Android status bar demo mode for {Serial}", _emulatorSerial);

        // Enable demo mode
        var enableResult = await RunAdbCommandAsync(
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
            var result = await RunAdbCommandAsync(command, timeout: TimeSpan.FromMinutes(1), cancellationToken: cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Failed to set status bar element: {Error}", result.StandardError);
            }
        }

        _logger.LogDebug("Status bar demo mode configured for {Serial}", _emulatorSerial);
    }

    /// <inheritdoc />
    public async Task InstallAppAsync(string apkPath, CancellationToken cancellationToken = default)
    {
        if (_emulatorSerial == null)
        {
            throw new InvalidOperationException("No emulator is currently active. Start an emulator first.");
        }

        DeviceHelpers.ValidateFilePath(apkPath, "Android APK");
        _logger.LogInformation("Installing Android app {ApkPath} on {Serial}", apkPath, _emulatorSerial);

        var result = await RunAdbCommandAsync(
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
    public async Task TakeScreenshotAsync(string outputFilePath, CancellationToken cancellationToken = default)
    {
        if (_emulatorSerial == null)
        {
            throw new InvalidOperationException("No emulator is currently active. Start an emulator first.");
        }

        DeviceHelpers.EnsureDirectoryExists(Path.GetDirectoryName(outputFilePath)!);
        _logger.LogDebug("Taking Android screenshot: {OutputPath}", outputFilePath);

        // Take screenshot on device
        var tempPath = "/sdcard/screenshot.png";
        var screenshotResult = await RunAdbCommandAsync(
            ["shell", "screencap", "-p", tempPath],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!screenshotResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to take screenshot: {screenshotResult.StandardError}");
        }

        // Pull screenshot to host
        var pullResult = await RunAdbCommandAsync(
            ["pull", tempPath, outputFilePath],
            timeout: TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (!pullResult.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to pull screenshot: {pullResult.StandardError}");
        }

        // Clean up temp file
        await RunAdbCommandAsync(
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
    public async Task ResetAppDataAsync(string packageName, CancellationToken cancellationToken = default)
    {
        if (_emulatorSerial == null)
        {
            throw new InvalidOperationException("No emulator is currently active. Start an emulator first.");
        }

        _logger.LogInformation("Resetting app data for {PackageName} on {Serial}", packageName, _emulatorSerial);

        var result = await RunAdbCommandAsync(
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
    public async Task StopEmulatorAsync(CancellationToken cancellationToken = default)
    {
        if (_emulatorSerial == null)
        {
            _logger.LogInformation("No emulator is currently active");
            return;
        }

        _logger.LogInformation("Stopping Android emulator {Serial}", _emulatorSerial);

        var result = await RunAdbCommandAsync(
            ["emu", "kill"],
            timeout: TimeSpan.FromMinutes(2),
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to stop emulator gracefully: {Error}", result.StandardError);
        }

        _emulatorSerial = null;
        _currentAvd = null;
        _logger.LogDebug("Emulator stop command completed");
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
        string[] args,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var fullArgs = new List<string>();
        
        if (_emulatorSerial != null)
        {
            fullArgs.AddRange(["-s", _emulatorSerial]);
        }
        
        fullArgs.AddRange(args);

        return await _commandRunner.RunAsync(
            "adb",
            fullArgs.ToArray(),
            timeout: timeout,
            cancellationToken: cancellationToken);
    }

    private async Task<int> FindAvailableEmulatorPortAsync(CancellationToken cancellationToken)
    {
        // Find an available port for the emulator (starting from 5554)
        for (int port = 5554; port < 5600; port += 2) // Emulator uses even ports
        {
            var result = await _commandRunner.RunAsync(
                "adb",
                ["devices"],
                timeout: TimeSpan.FromSeconds(10),
                cancellationToken: cancellationToken);

            if (result.IsSuccess && !result.StandardOutput.Contains($"emulator-{port}"))
            {
                return port;
            }
        }

        throw new InvalidOperationException("No available emulator ports found");
    }

    private async Task<bool> IsEmulatorStartingAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync(
                "adb",
                ["devices"],
                timeout: TimeSpan.FromSeconds(10),
                cancellationToken: cancellationToken);

            return result.IsSuccess && result.StandardOutput.Contains(_emulatorSerial ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while checking emulator startup status");
            return false;
        }
    }

    private async Task<bool> IsEmulatorBootedAsync(CancellationToken cancellationToken)
    {
        if (_emulatorSerial == null) return false;

        try
        {
            var result = await RunAdbCommandAsync(
                ["shell", "getprop", "sys.boot_completed"],
                timeout: TimeSpan.FromSeconds(10),
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
        // On different platforms, the emulator command might be in different locations
        if (Os.IsWindows)
        {
            return "emulator.exe";
        }
        else
        {
            return "emulator";
        }
    }
}