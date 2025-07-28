using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium.Appium;
using Snappium.Core.Abstractions;
using Snappium.Core.Appium;
using Snappium.Core.Build;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Snappium.Core.Logging;
using Snappium.Core.Planning;

namespace Snappium.Core.Orchestration;

/// <summary>
/// Main orchestrator for screenshot automation workflows.
/// </summary>
public sealed class Orchestrator : IOrchestrator
{
    private readonly IDriverFactory _driverFactory;
    private readonly IActionExecutor _actionExecutor;
    private readonly IImageValidator _imageValidator;
    private readonly IIosDeviceManager _iosDeviceManager;
    private readonly IAndroidDeviceManager _androidDeviceManager;
    private readonly IBuildService _buildService;
    private readonly IAppiumServerController _appiumServerController;
    private readonly PortAllocator _portAllocator;
    private readonly ILogger<Orchestrator> _logger;
    private readonly ISnappiumLogger? _snappiumLogger;

    public Orchestrator(
        IDriverFactory driverFactory,
        IActionExecutor actionExecutor,
        IImageValidator imageValidator,
        IIosDeviceManager iosDeviceManager,
        IAndroidDeviceManager androidDeviceManager,
        IBuildService buildService,
        IAppiumServerController appiumServerController,
        PortAllocator portAllocator,
        ILogger<Orchestrator> logger,
        ISnappiumLogger? snappiumLogger = null)
    {
        _driverFactory = driverFactory;
        _actionExecutor = actionExecutor;
        _imageValidator = imageValidator;
        _iosDeviceManager = iosDeviceManager;
        _androidDeviceManager = androidDeviceManager;
        _buildService = buildService;
        _appiumServerController = appiumServerController;
        _portAllocator = portAllocator;
        _logger = logger;
        _snappiumLogger = snappiumLogger;
    }

    /// <inheritdoc />
    public async Task<RunResult> ExecuteAsync(
        RunPlan runPlan,
        RootConfig config,
        CliOverrides? cliOverrides = null,
        CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTimeOffset.UtcNow;
        
        _logger.LogInformation("Starting screenshot run {RunId} with {JobCount} jobs", runId, runPlan.Jobs.Count);

        var jobResults = new List<JobResult>();
        var overallSuccess = true;
        string? runErrorMessage = null;

        try
        {
            // Execute each job serially (parallel execution can be added later)
            foreach (var job in runPlan.Jobs)
            {
                try
                {
                    var jobResult = await ExecuteJobAsync(job, config, cliOverrides, cancellationToken);
                    jobResults.Add(jobResult);
                    
                    if (!jobResult.Success)
                    {
                        overallSuccess = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute job for {Platform} {Device}", job.Platform, job.DeviceFolder);
                    
                    jobResults.Add(new JobResult
                    {
                        Job = job,
                        Status = JobStatus.Failed,
                        StartTime = DateTimeOffset.UtcNow,
                        EndTime = DateTimeOffset.UtcNow,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        Screenshots = new List<ScreenshotResult>(),
                        FailureArtifacts = new List<FailureArtifact>()
                    });
                    
                    overallSuccess = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} failed with exception", runId);
            overallSuccess = false;
            runErrorMessage = ex.Message;
        }

        var endTime = DateTimeOffset.UtcNow;
        
        var runResult = new RunResult
        {
            RunId = runId,
            StartTime = startTime,
            EndTime = endTime,
            Success = overallSuccess,
            JobResults = jobResults,
            Environment = GetEnvironmentInfo(),
            ErrorMessage = runErrorMessage
        };

        _logger.LogInformation("Completed run {RunId} in {Duration}ms. Success: {Success}",
            runId, runResult.Duration.TotalMilliseconds, overallSuccess);

        return runResult;
    }

    private async Task<JobResult> ExecuteJobAsync(
        RunJob job,
        RootConfig config,
        CliOverrides? cliOverrides,
        CancellationToken cancellationToken)
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

        try
        {
            // Assign ports for this job (need job index, using 0 for now)
            var ports = _portAllocator.AllocatePortsForJob(0);
            _snappiumLogger?.LogDebug("Allocated ports: Appium={0}", ports.AppiumPort);

            // Prepare device and get device identifier
            _snappiumLogger?.LogInfo("Preparing device...");
            deviceIdentifier = await PrepareDeviceAsync(job, config, cancellationToken);

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
            var deviceSerial = await _androidDeviceManager.StartEmulatorAsync(job.AndroidDevice.Avd, cancellationToken);
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
            _logger.LogDebug("Skipping build due to --no-build flag");
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

            // Capture device logs
            try
            {
                var logsPath = Path.Combine(artifactDir, "device_logs.txt");
                string logs = "";

                if (job.Platform == Platform.iOS && job.IosDevice != null)
                {
                    // iOS logs via simctl log show (simplified)
                    logs = "iOS logs would be captured here";
                }
                else if (job.Platform == Platform.Android)
                {
                    // Android logs via logcat (simplified)
                    logs = "Android logcat would be captured here";
                }

                await File.WriteAllTextAsync(logsPath, logs, cancellationToken);
                
                failureArtifacts.Add(new FailureArtifact
                {
                    Type = FailureArtifactType.DeviceLogs,
                    Path = logsPath,
                    Timestamp = DateTimeOffset.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to capture device logs");
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

    private static EnvironmentInfo GetEnvironmentInfo()
    {
        return new EnvironmentInfo
        {
            OperatingSystem = RuntimeInformation.OSDescription,
            DotNetVersion = RuntimeInformation.FrameworkDescription,
            Hostname = Environment.MachineName,
            WorkingDirectory = Environment.CurrentDirectory,
            SnappiumVersion = typeof(Orchestrator).Assembly.GetName().Version?.ToString() ?? "unknown"
        };
    }
}