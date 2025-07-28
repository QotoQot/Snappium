using OpenQA.Selenium.Appium;
using Snappium.Core.Abstractions;
using Snappium.Core.Config;

namespace Snappium.Core.Appium;

/// <summary>
/// Interface for executing screenshot automation actions.
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// Execute all actions in a screenshot plan.
    /// </summary>
    /// <param name="driver">Appium driver</param>
    /// <param name="job">Run job with device and language configuration</param>
    /// <param name="deviceIdentifier">Device identifier (UDID for iOS, serial for Android)</param>
    /// <param name="screenshotPlan">Screenshot plan with actions to execute</param>
    /// <param name="outputDirectory">Directory for saving screenshots</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results of screenshot captures</returns>
    Task<List<ScreenshotResult>> ExecuteAsync(
        AppiumDriver driver,
        RunJob job,
        string deviceIdentifier,
        ScreenshotPlan screenshotPlan,
        string outputDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute dismissor actions to handle popups and alerts.
    /// </summary>
    /// <param name="driver">Appium driver</param>
    /// <param name="dismissors">List of dismissor selectors</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of dismissors successfully executed</returns>
    Task<int> ExecuteDismissorsAsync(
        AppiumDriver driver,
        List<Selector>? dismissors,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate assertions for a screenshot plan.
    /// </summary>
    /// <param name="driver">Appium driver</param>
    /// <param name="assertions">Platform-specific assertions to validate</param>
    /// <param name="platform">Current platform</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if all assertions pass</returns>
    Task<bool> ValidateAssertionsAsync(
        AppiumDriver driver,
        PlatformAssertions? assertions,
        Platform platform,
        CancellationToken cancellationToken = default);
}