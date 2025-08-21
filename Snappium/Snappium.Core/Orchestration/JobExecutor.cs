using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium.Appium;
using Snappium.Core.Abstractions;
using Snappium.Core.Appium;
using Snappium.Core.Build;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Snappium.Core.Infrastructure;
using Snappium.Core.Logging;

namespace Snappium.Core.Orchestration;

/// <summary>
/// Executes individual screenshot automation jobs with proper resource management and error handling.
/// </summary>
public sealed class JobExecutor : IJobExecutor
{
    private readonly IDriverFactory _driverFactory;
    private readonly IActionExecutor _actionExecutor;
    private readonly IImageValidator _imageValidator;
    private readonly IIosDeviceManager _iosDeviceManager;
    private readonly IAndroidDeviceManager _androidDeviceManager;
    private readonly IBuildService _buildService;
    private readonly IAppiumServerController _appiumServerController;
    private readonly ProcessManager _processManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutor> _logger;
    private readonly ISnappiumLogger? _snappiumLogger;

    public JobExecutor(
        IDriverFactory driverFactory,
        IActionExecutor actionExecutor,
        IImageValidator imageValidator,
        IIosDeviceManager iosDeviceManager,
        IAndroidDeviceManager androidDeviceManager,
        IBuildService buildService,
        IAppiumServerController appiumServerController,
        ProcessManager processManager,
        IServiceProvider serviceProvider,
        ILogger<JobExecutor> logger,
        ISnappiumLogger? snappiumLogger = null)
    {
        _driverFactory = driverFactory;
        _actionExecutor = actionExecutor;
        _imageValidator = imageValidator;
        _iosDeviceManager = iosDeviceManager;
        _androidDeviceManager = androidDeviceManager;
        _buildService = buildService;
        _appiumServerController = appiumServerController;
        _processManager = processManager;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _snappiumLogger = snappiumLogger;
    }

    /// <inheritdoc />
    public async Task<JobResult> ExecuteAsync(
        RunJob job,
        RootConfig config,
        CliOverrides? cliOverrides = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var jobId = $"{job.Platform}-{job.DeviceFolder}-{job.Language}";
        
        _logger.LogInformation("Executing job: {Platform} {Device} {Language}",
            job.Platform, job.DeviceFolder, job.Language);

        // Begin job-scoped logging
        using var jobScope = _snappiumLogger?.BeginJobScope(jobId);
        _snappiumLogger?.LogInfo("Starting job execution: {0} {1} {2}", job.Platform, job.DeviceFolder, job.Language);

        var screenshots = new List<ScreenshotResult>();
        var failureArtifacts = new List<FailureArtifact>();
        var success = true;
        string? errorMessage = null;
        string? deviceIdentifier = null;
        string? appiumProcessId = null;

        try
        {
            // Use pre-allocated ports from the RunJob (assigned during planning phase)
            var ports = job.Ports;
            _snappiumLogger?.LogDebug("Using pre-allocated ports: Appium={0}, SystemPort={1}, WdaLocalPort={2}", 
                ports.AppiumPort, ports.SystemPort, ports.WdaLocalPort);

            // Start Appium server and register with ProcessManager
            _snappiumLogger?.LogInfo("Starting Appium server on port {0}...", ports.AppiumPort);
            var serverResult = await _appiumServerController.StartServerAsync(ports.AppiumPort, cancellationToken);
            if (!serverResult.Success)
            {
                throw new InvalidOperationException($"Failed to start Appium server on port {ports.AppiumPort}: {serverResult.ErrorMessage}");
            }

            appiumProcessId = $"appium-{ports.AppiumPort}";
            var appiumLogger = _serviceProvider.GetRequiredService<ILogger<ManagedAppiumServer>>();
            var managedAppiumServer = new ManagedAppiumServer(_appiumServerController, ports.AppiumPort, appiumLogger);
            _processManager.RegisterProcess(appiumProcessId, managedAppiumServer);

            try
            {
                // Prepare device and get device identifier
                _snappiumLogger?.LogInfo("Preparing device...");
                deviceIdentifier = await PrepareDeviceAsync(job, config, cancellationToken);
                
                // Register managed device with ProcessManager
                RegisterManagedDevice(job, deviceIdentifier);

                // Build and install app if needed
                _snappiumLogger?.LogInfo("Building and installing app...");
                await BuildAndInstallAppAsync(job, config, cliOverrides, deviceIdentifier, cancellationToken);

                // Create Appium driver and execute screenshot plans
                var serverUrl = cliOverrides?.ServerUrl ?? $"http://localhost:{ports.AppiumPort}";
                _snappiumLogger?.LogInfo("Creating Appium driver for {0}", serverUrl);
                using var driver = await _driverFactory.CreateDriverAsync(job, serverUrl, cancellationToken);

            _snappiumLogger?.LogInfo("Executing {0} screenshot plans", job.Screenshots.Count);
            foreach (var screenshotPlan in job.Screenshots)
            {
                try
                {
                    _logger.LogDebug("Executing screenshot plan: {Name}", screenshotPlan.Name);
                    _snappiumLogger?.LogInfo("Taking screenshot: {0}", screenshotPlan.Name);
                    
                    var planResults = await _actionExecutor.ExecuteAsync(
                        driver, job, deviceIdentifier, screenshotPlan, job.OutputDirectory, cancellationToken);
                    
                    screenshots.AddRange(planResults);
                    _snappiumLogger?.LogSuccess("Screenshot completed: {0} ({1} files)", screenshotPlan.Name, planResults.Count);

                    // Validate screenshots if configured
                    foreach (var screenshot in planResults)
                    {
                        if (config.Validation != null)
                        {
                            var validationResult = await _imageValidator.ValidateAsync(
                                screenshot, job.DeviceFolder, config.Validation, cancellationToken);

                            if (!validationResult.IsValid && config.Validation.EnforceImageSize == true)
                            {
                                _logger.LogError("Screenshot validation failed: {Error}", validationResult.ErrorMessage);
                                _snappiumLogger?.LogError("Screenshot validation failed: {0}", validationResult.ErrorMessage);
                                success = false;
                                errorMessage = validationResult.ErrorMessage;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute screenshot plan: {Name}", screenshotPlan.Name);
                    _snappiumLogger?.LogError(ex, "Failed to execute screenshot plan: {0}", screenshotPlan.Name);
                    success = false;
                    errorMessage = ex.Message;

                    // Capture failure artifacts
                    _snappiumLogger?.LogWarning("Capturing failure artifacts...");
                    await CaptureFailureArtifactsAsync(driver, job, deviceIdentifier, failureArtifacts, cancellationToken);
                }
            }
            }
            finally
            {
                // Unregister and cleanup managed processes
                _snappiumLogger?.LogInfo("Cleaning up managed processes...");
                await UnregisterManagedDeviceAsync(job, deviceIdentifier, cancellationToken);
                if (appiumProcessId != null)
                {
                    _processManager.UnregisterProcess(appiumProcessId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job failed for {Platform} {Device}", job.Platform, job.DeviceFolder);
            _snappiumLogger?.LogError(ex, "Job failed for {0} {1}", job.Platform, job.DeviceFolder);
            success = false;
            errorMessage = ex.Message;
        }
        finally
        {
            // Clean up device
            _snappiumLogger?.LogInfo("Cleaning up device...");
            await CleanupDeviceAsync(job, deviceIdentifier, cancellationToken);
        }

        var endTime = DateTimeOffset.UtcNow;
        var duration = endTime - startTime;
        
        if (success)
        {
            _snappiumLogger?.LogSuccess("Job completed successfully in {0:F1}s ({1} screenshots)", 
                duration.TotalSeconds, screenshots.Count);
        }
        else
        {
            _snappiumLogger?.LogError("Job failed after {0:F1}s: {1}", 
                duration.TotalSeconds, errorMessage ?? "Unknown error");
        }

        return new JobResult
        {
            Job = job,
            Status = success ? JobStatus.Success : JobStatus.Failed,
            StartTime = startTime,
            EndTime = endTime,
            Screenshots = screenshots,
            FailureArtifacts = failureArtifacts,
            ErrorMessage = errorMessage
        };
    }

    private async Task<string> PrepareDeviceAsync(RunJob job, RootConfig config, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Preparing {Platform} device", job.Platform);

        if (job.Platform == Platform.iOS && job.IosDevice != null)
        {
            var udidOrName = DeviceHelpers.GetDeviceIdentifier(job.IosDevice.Udid, job.IosDevice.Name);
            
            // iOS preparation sequence
            await _iosDeviceManager.ShutdownAsync(udidOrName, cancellationToken);
            await _iosDeviceManager.SetLanguageAsync(udidOrName, job.Language, job.LocaleMapping, cancellationToken);
            await _iosDeviceManager.BootAsync(udidOrName, cancellationToken);
            
            if (config.StatusBar?.Ios != null)
            {
                await _iosDeviceManager.SetStatusBarAsync(udidOrName, config.StatusBar.Ios, cancellationToken);
            }

            if (config.AppReset?.Policy == "always")
            {
                var bundleId = await DeviceHelpers.ExtractIosBundleIdAsync(job.AppPath);
                await _iosDeviceManager.ResetAppDataAsync(udidOrName, bundleId, cancellationToken);
            }
            
            return udidOrName;
        }
        else if (job.Platform == Platform.Android && job.AndroidDevice != null)
        {
            // Android preparation sequence
            var emulatorStartPort = config.Ports?.EmulatorStartPort ?? Defaults.Ports.EmulatorStartPort;
            var emulatorEndPort = config.Ports?.EmulatorEndPort ?? Defaults.Ports.EmulatorEndPort;
            var deviceSerial = await _androidDeviceManager.StartEmulatorAsync(job.AndroidDevice.Avd, emulatorStartPort, emulatorEndPort, cancellationToken);
            await _androidDeviceManager.WaitForBootAsync(deviceSerial, timeout: null, cancellationToken);
            await _androidDeviceManager.SetLanguageAsync(deviceSerial, job.Language, job.LocaleMapping, cancellationToken);
            
            if (config.StatusBar?.Android != null)
            {
                await _androidDeviceManager.SetStatusBarDemoModeAsync(deviceSerial, config.StatusBar.Android, cancellationToken);
            }

            if (config.AppReset?.Policy == "always")
            {
                var bundleId = DeviceHelpers.ExtractAndroidPackageName(job.AppPath);
                await _androidDeviceManager.ResetAppDataAsync(deviceSerial, bundleId, cancellationToken);
            }
            
            return deviceSerial;
        }
        
        throw new InvalidOperationException($"Unsupported platform: {job.Platform}");
    }

    private async Task BuildAndInstallAppAsync(
        RunJob job,
        RootConfig config,
        CliOverrides? cliOverrides,
        string deviceIdentifier,
        CancellationToken cancellationToken)
    {
        if (cliOverrides?.NoBuild == true)
        {
            _logger.LogDebug("Skipping build due to --build never flag");
            return;
        }

        string? appPath = job.AppPath;

        // Use CLI override paths if provided
        if (job.Platform == Platform.iOS && !string.IsNullOrEmpty(cliOverrides?.IosAppPath))
        {
            appPath = cliOverrides.IosAppPath;
            _logger.LogDebug("Using CLI override iOS app path: {Path}", cliOverrides.IosAppPath);
        }
        else if (job.Platform == Platform.Android && !string.IsNullOrEmpty(cliOverrides?.AndroidAppPath))
        {
            appPath = cliOverrides.AndroidAppPath;
            _logger.LogDebug("Using CLI override Android app path: {Path}", cliOverrides.AndroidAppPath);
        }
        else if (config.BuildConfig != null)
        {
            // Build the project
            var platformBuildConfig = job.Platform == Platform.iOS ? config.BuildConfig.Ios : config.BuildConfig.Android;
            if (platformBuildConfig?.Csproj != null)
            {
                var buildResult = await _buildService.BuildAsync(
                    job.Platform,
                    platformBuildConfig.Csproj,
                    cliOverrides?.BuildConfiguration ?? "Release",
                    cancellationToken: cancellationToken);

                if (!buildResult.Success)
                {
                    throw new InvalidOperationException($"Build failed: {buildResult.ErrorMessage}");
                }

                // Discover the built artifact
                var searchPattern = job.Platform == Platform.iOS ? "*.app" : "*.apk";
                var artifactPath = await _buildService.DiscoverArtifactAsync(searchPattern, buildResult.OutputDirectory);
                
                if (string.IsNullOrEmpty(artifactPath))
                {
                    throw new InvalidOperationException($"Could not find {searchPattern} artifact in {buildResult.OutputDirectory}");
                }

                appPath = artifactPath;
                _logger.LogInformation("Built and discovered app: {Path}", artifactPath);
            }
        }

        // Install the app
        if (!string.IsNullOrEmpty(appPath))
        {
            if (job.Platform == Platform.iOS && job.IosDevice != null)
            {
                await _iosDeviceManager.InstallAppAsync(deviceIdentifier, appPath, cancellationToken);
            }
            else if (job.Platform == Platform.Android)
            {
                await _androidDeviceManager.InstallAppAsync(deviceIdentifier, appPath, cancellationToken);
            }
        }
    }

    private async Task CaptureFailureArtifactsAsync(
        AppiumDriver driver,
        RunJob job,
        string deviceIdentifier,
        List<FailureArtifact> failureArtifacts,
        CancellationToken cancellationToken)
    {
        try
        {
            var artifactDir = Path.Combine(job.OutputDirectory, "failure_artifacts");
            Directory.CreateDirectory(artifactDir);

            // Capture page source
            try
            {
                var pageSource = driver.PageSource;
                var pageSourcePath = Path.Combine(artifactDir, "page_source.xml");
                await File.WriteAllTextAsync(pageSourcePath, pageSource, cancellationToken);
                
                failureArtifacts.Add(new FailureArtifact
                {
                    Type = FailureArtifactType.PageSource,
                    Path = pageSourcePath,
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to capture page source");
            }

            // Capture failure screenshot
            try
            {
                var screenshotPath = Path.Combine(artifactDir, "failure_screenshot.png");
                
                if (job.Platform == Platform.iOS && job.IosDevice != null)
                {
                    await _iosDeviceManager.TakeScreenshotAsync(deviceIdentifier, screenshotPath, cancellationToken);
                }
                else if (job.Platform == Platform.Android)
                {
                    await _androidDeviceManager.TakeScreenshotAsync(deviceIdentifier, screenshotPath, cancellationToken);
                }

                failureArtifacts.Add(new FailureArtifact
                {
                    Type = FailureArtifactType.Screenshot,
                    Path = screenshotPath,
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to capture failure screenshot");
            }
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture failure artifacts");
        }
    }

    private async Task CleanupDeviceAsync(RunJob job, string? deviceIdentifier, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Cleaning up {Platform} device", job.Platform);

            if (job.Platform == Platform.iOS && job.IosDevice != null)
            {
                var udidOrName = DeviceHelpers.GetDeviceIdentifier(job.IosDevice.Udid, job.IosDevice.Name);
                await _iosDeviceManager.ShutdownAsync(udidOrName, cancellationToken);
            }
            else if (job.Platform == Platform.Android && job.AndroidDevice != null && deviceIdentifier != null)
            {
                await _androidDeviceManager.StopEmulatorAsync(deviceIdentifier, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during device cleanup for {Platform} {Device}", job.Platform, job.DeviceFolder);
        }
    }

    private void RegisterManagedDevice(RunJob job, string deviceIdentifier)
    {
        if (job.Platform == Platform.iOS && job.IosDevice != null)
        {
            var deviceProcessId = $"ios-simulator-{deviceIdentifier}";
            var iosLogger = _serviceProvider.GetRequiredService<ILogger<ManagedIosSimulator>>();
            var managedDevice = new ManagedIosSimulator(_iosDeviceManager, deviceIdentifier, iosLogger);
            _processManager.RegisterProcess(deviceProcessId, managedDevice);
        }
        else if (job.Platform == Platform.Android && job.AndroidDevice != null)
        {
            var deviceProcessId = $"android-emulator-{deviceIdentifier}";
            var androidLogger = _serviceProvider.GetRequiredService<ILogger<ManagedAndroidEmulator>>();
            var managedDevice = new ManagedAndroidEmulator(_androidDeviceManager, deviceIdentifier, androidLogger);
            _processManager.RegisterProcess(deviceProcessId, managedDevice);
        }
    }

    private async Task UnregisterManagedDeviceAsync(RunJob job, string? deviceIdentifier, CancellationToken cancellationToken)
    {
        if (deviceIdentifier == null) return;

        if (job.Platform == Platform.iOS && job.IosDevice != null)
        {
            var deviceProcessId = $"ios-simulator-{deviceIdentifier}";
            _processManager.UnregisterProcess(deviceProcessId);
        }
        else if (job.Platform == Platform.Android && job.AndroidDevice != null)
        {
            var deviceProcessId = $"android-emulator-{deviceIdentifier}";
            _processManager.UnregisterProcess(deviceProcessId);
        }
    }
}