using OpenQA.Selenium.Appium;
using Snappium.Core.Abstractions;

namespace Snappium.Core.Appium;

/// <summary>
/// Factory interface for creating Appium drivers with appropriate configuration.
/// </summary>
public interface IDriverFactory
{
    /// <summary>
    /// Create an Appium driver for the specified job.
    /// </summary>
    /// <param name="job">Run job containing device and port configuration</param>
    /// <param name="serverUrl">Appium server URL (defaults to http://localhost:4723)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configured Appium driver</returns>
    Task<AppiumDriver> CreateDriverAsync(
        RunJob job, 
        string? serverUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Safely dispose of an Appium driver, quitting the session.
    /// </summary>
    /// <param name="driver">Driver to dispose</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when driver is disposed</returns>
    Task DisposeDriverAsync(AppiumDriver driver, CancellationToken cancellationToken = default);
}