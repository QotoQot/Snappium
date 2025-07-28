using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using Snappium.Core.Config;

namespace Snappium.Core.Appium;

/// <summary>
/// Interface for finding elements using various selector strategies.
/// </summary>
public interface IElementFinder
{
    /// <summary>
    /// Find an element using the specified selector.
    /// </summary>
    /// <param name="driver">Appium driver</param>
    /// <param name="selector">Selector configuration</param>
    /// <param name="timeout">Maximum time to wait for element</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Found element</returns>
    Task<IWebElement> FindElementAsync(
        AppiumDriver driver,
        Selector selector,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Wait for an element to be present using the specified selector.
    /// </summary>
    /// <param name="driver">Appium driver</param>
    /// <param name="selector">Selector configuration</param>
    /// <param name="timeout">Maximum time to wait</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if element is found, false if timeout</returns>
    Task<bool> WaitForElementAsync(
        AppiumDriver driver,
        Selector selector,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert a selector to a Selenium By locator.
    /// </summary>
    /// <param name="selector">Selector configuration</param>
    /// <returns>Selenium By locator</returns>
    By GetByFromSelector(Selector selector);
}