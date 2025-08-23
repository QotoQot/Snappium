using Microsoft.Extensions.Logging;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.iOS;
using Snappium.Core.Abstractions;
using Snappium.Core.DeviceManagement;

namespace Snappium.Core.Appium;

/// <summary>
/// Factory for creating and managing Appium drivers with platform-specific configuration.
/// </summary>
public sealed class DriverFactory : IDriverFactory
{
    readonly IIosDeviceManager _iosDeviceManager;
    readonly IAndroidDeviceManager _androidDeviceManager;
    readonly ILogger<DriverFactory> _logger;

    public DriverFactory(
        IIosDeviceManager iosDeviceManager,
        IAndroidDeviceManager androidDeviceManager,
        ILogger<DriverFactory> logger)
    {
        _iosDeviceManager = iosDeviceManager;
        _androidDeviceManager = androidDeviceManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AppiumDriver> CreateDriverAsync(
        RunJob job, 
        string? serverUrl = null,
        CancellationToken cancellationToken = default)
    {
        serverUrl ??= $"http://localhost:{job.Ports.AppiumPort}";
        
        _logger.LogInformation("Creating {Platform} driver for {Device} on {ServerUrl}",
            job.Platform, 
            job.Platform == Platform.iOS ? job.IosDevice?.Name : job.AndroidDevice?.Name,
            serverUrl);

        try
        {
            var appiumOptions = CreateAppiumOptions(job);
            var serverUri = new Uri(serverUrl);

            AppiumDriver driver = job.Platform switch
            {
                Platform.iOS => new IOSDriver(serverUri, appiumOptions, TimeSpan.FromMinutes(5)),
                Platform.Android => new AndroidDriver(serverUri, appiumOptions, TimeSpan.FromMinutes(5)),
                _ => throw new ArgumentException($"Unsupported platform: {job.Platform}")
            };

            // Set implicit wait to 0 - all waits will be explicit
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;

            _logger.LogDebug("Driver created successfully for {Platform}", job.Platform);
            
            // Small delay to ensure driver is fully initialized
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            
            return driver;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create {Platform} driver", job.Platform);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DisposeDriverAsync(AppiumDriver driver, CancellationToken cancellationToken = default)
    {
        if (driver == null)
        {
            return;
        }

        _logger.LogDebug("Disposing Appium driver");

        try
        {
            driver.Quit();
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            _logger.LogDebug("Driver disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while disposing driver");
        }
        finally
        {
            driver.Dispose();
        }
    }

    AppiumOptions CreateAppiumOptions(RunJob job)
    {
        var options = new AppiumOptions();
        
        // Get capabilities from device manager
        Dictionary<string, object> capabilities = job.Platform switch
        {
            Platform.iOS => _iosDeviceManager.GetCapabilities(
                job.IosDevice ?? throw new InvalidOperationException("iOS device is required"),
                job.Language,
                job.LocaleMapping,
                job.AppPath),
            
            Platform.Android => _androidDeviceManager.GetCapabilities(
                job.AndroidDevice ?? throw new InvalidOperationException("Android device is required"),
                job.Language,
                job.LocaleMapping,
                job.AppPath),
            
            _ => throw new ArgumentException($"Unsupported platform: {job.Platform}")
        };

        // Add capabilities to options, handling special built-in properties
        foreach (var (key, value) in capabilities)
        {
            switch (key)
            {
                case "platformName":
                    options.PlatformName = value?.ToString();
                    break;
                case "platformVersion":
                    options.PlatformVersion = value?.ToString();
                    break;
                case "deviceName":
                    options.DeviceName = value?.ToString();
                    break;
                case "automationName":
                    options.AutomationName = value?.ToString();
                    break;
                case "app":
                    options.App = value?.ToString();
                    break;
                default:
                    options.AddAdditionalAppiumOption(key, value);
                    break;
            }
        }

        // Add port-specific capabilities if needed
        if (job.Platform == Platform.iOS)
        {
            options.AddAdditionalAppiumOption("wdaLocalPort", job.Ports.WdaLocalPort);
        }
        else if (job.Platform == Platform.Android)
        {
            options.AddAdditionalAppiumOption("systemPort", job.Ports.SystemPort);
        }

        _logger.LogDebug("Created Appium options with {Count} capabilities", capabilities.Count);

        return options;
    }
}