using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using Snappium.Core.Abstractions;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Platform = Snappium.Core.Abstractions.Platform;

namespace Snappium.Core.Appium;

/// <summary>
/// Executes screenshot automation actions using Appium.
/// </summary>
public sealed class ActionExecutor : IActionExecutor
{
    private readonly IElementFinder _elementFinder;
    private readonly IIosDeviceManager _iosDeviceManager;
    private readonly IAndroidDeviceManager _androidDeviceManager;
    private readonly ILogger<ActionExecutor> _logger;

    public ActionExecutor(
        IElementFinder elementFinder,
        IIosDeviceManager iosDeviceManager,
        IAndroidDeviceManager androidDeviceManager,
        ILogger<ActionExecutor> logger)
    {
        _elementFinder = elementFinder;
        _iosDeviceManager = iosDeviceManager;
        _androidDeviceManager = androidDeviceManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ScreenshotResult>> ExecuteAsync(
        AppiumDriver driver,
        RunJob job,
        string deviceIdentifier,
        ScreenshotPlan screenshotPlan,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing screenshot plan: {Name}", screenshotPlan.Name);
        var results = new List<ScreenshotResult>();

        try
        {
            // Set orientation if specified
            if (!string.IsNullOrEmpty(screenshotPlan.Orientation))
            {
                await SetOrientationAsync(driver, screenshotPlan.Orientation, cancellationToken);
            }

            // Execute dismissors before main actions
            if (job.Platform == Platform.iOS && screenshotPlan.Dismissors?.Ios != null)
            {
                await ExecuteDismissorsAsync(driver, screenshotPlan.Dismissors.Ios, cancellationToken);
            }
            else if (job.Platform == Platform.Android && screenshotPlan.Dismissors?.Android != null)
            {
                await ExecuteDismissorsAsync(driver, screenshotPlan.Dismissors.Android, cancellationToken);
            }

            // Execute each action
            foreach (var action in screenshotPlan.Actions)
            {
                // Execute the action (dismissors already ran once at the beginning)
                var result = await ExecuteActionAsync(driver, job, deviceIdentifier, action, outputDirectory, cancellationToken);
                if (result != null)
                {
                    results.Add(result);
                }
            }

            // Validate assertions after all actions
            if (screenshotPlan.Assert != null)
            {
                var assertionsValid = await ValidateAssertionsAsync(driver, screenshotPlan.Assert, job.Platform, cancellationToken);
                if (!assertionsValid)
                {
                    _logger.LogWarning("Assertions failed for screenshot plan: {Name}", screenshotPlan.Name);
                }
            }

            _logger.LogInformation("Completed screenshot plan: {Name} with {Count} screenshots", 
                screenshotPlan.Name, results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing screenshot plan: {Name}", screenshotPlan.Name);
            throw;
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteDismissorsAsync(
        AppiumDriver driver,
        List<Selector>? dismissors,
        CancellationToken cancellationToken = default)
    {
        if (dismissors == null || dismissors.Count == 0)
        {
            return 0;
        }

        _logger.LogDebug("Executing {Count} dismissors", dismissors.Count);
        int successCount = 0;

        foreach (var dismissor in dismissors)
        {
            try
            {
                var element = await _elementFinder.FindElementAsync(
                    driver, dismissor, TimeSpan.FromSeconds(2), cancellationToken);
                
                element.Click();
                successCount++;
                _logger.LogDebug("Successfully dismissed element");
                
                // Small delay after dismissing
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
            catch (Exception ex) when (ex is TimeoutException || ex is NoSuchElementException)
            {
                // Dismissors are best-effort, so we don't fail on missing elements
                _logger.LogDebug("Dismissor element not found (this is okay): {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error executing dismissor");
            }
        }

        return successCount;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateAssertionsAsync(
        AppiumDriver driver,
        PlatformAssertions? assertions,
        Platform platform,
        CancellationToken cancellationToken = default)
    {
        if (assertions == null)
        {
            return true;
        }

        var selector = platform switch
        {
            Platform.iOS => assertions.Ios,
            Platform.Android => assertions.Android,
            _ => null
        };

        if (selector == null)
        {
            return true;
        }

        _logger.LogDebug("Validating assertion for {Platform}", platform);

        try
        {
            var element = await _elementFinder.FindElementAsync(
                driver, selector, Defaults.Timeouts.ElementOperation, cancellationToken);
            
            _logger.LogDebug("Assertion validated - element found");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assertion failed - element not found");
            return false;
        }
    }

    private async Task<ScreenshotResult?> ExecuteActionAsync(
        AppiumDriver driver,
        RunJob job,
        string deviceIdentifier,
        ScreenshotAction action,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing action: {ActionType}", GetActionType(action));

        // Handle tap action
        if (action.Tap != null)
        {
            var element = await _elementFinder.FindElementAsync(
                driver, action.Tap, cancellationToken: cancellationToken);
            element.Click();
            _logger.LogDebug("Tapped element");
            return null;
        }

        // Handle wait action
        if (action.Wait != null)
        {
            var seconds = action.Wait.Seconds ?? 1;
            _logger.LogDebug("Waiting for {Seconds} seconds", seconds);
            await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
            return null;
        }

        // Handle wait_for action
        if (action.WaitFor != null)
        {
            var timeout = TimeSpan.FromSeconds(action.WaitFor.Timeout ?? (int)Defaults.Timeouts.ElementOperation.TotalSeconds);
            var found = await _elementFinder.WaitForElementAsync(
                driver, action.WaitFor.Selector, timeout, cancellationToken);
            
            if (!found)
            {
                throw new TimeoutException($"Element not found within {timeout}");
            }
            
            _logger.LogDebug("Wait for element completed");
            return null;
        }

        // Handle capture action
        if (action.Capture != null)
        {
            var fileName = $"{action.Capture.Name}_{job.Language}.png";
            var filePath = Path.Combine(outputDirectory, fileName);
            
            _logger.LogInformation("Capturing screenshot: {FileName}", fileName);

            // Take screenshot using device manager
            if (job.Platform == Platform.iOS)
            {
                await _iosDeviceManager.TakeScreenshotAsync(deviceIdentifier, filePath, cancellationToken);
            }
            else if (job.Platform == Platform.Android)
            {
                await _androidDeviceManager.TakeScreenshotAsync(deviceIdentifier, filePath, cancellationToken);
            }

            return new ScreenshotResult
            {
                Name = action.Capture.Name,
                Path = filePath,
                Timestamp = DateTimeOffset.UtcNow,
                Success = true
            };
        }

        _logger.LogWarning("Unknown action type encountered");
        return null;
    }

    private async Task SetOrientationAsync(AppiumDriver driver, string orientation, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Setting orientation to: {Orientation}", orientation);

        try
        {
            var screenOrientation = orientation.ToLowerInvariant() switch
            {
                "portrait" => ScreenOrientation.Portrait,
                "landscape" => ScreenOrientation.Landscape,
                _ => throw new ArgumentException($"Invalid orientation: {orientation}")
            };

            driver.Orientation = screenOrientation;
            
            // Wait for orientation change to take effect
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            
            _logger.LogDebug("Orientation set successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set orientation");
            throw;
        }
    }

    private static string GetActionType(ScreenshotAction action)
    {
        if (action.Tap != null) return "Tap";
        if (action.Wait != null) return "Wait";
        if (action.WaitFor != null) return "WaitFor";
        if (action.Capture != null) return "Capture";
        return "Unknown";
    }
}