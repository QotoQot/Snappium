using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using Snappium.Core.Config;
using SeleniumExtras.WaitHelpers;

namespace Snappium.Core.Appium;

/// <summary>
/// Implementation of element finding with various selector strategies.
/// </summary>
public sealed class ElementFinder : IElementFinder
{
    private readonly ILogger<ElementFinder> _logger;
    private static readonly TimeSpan DefaultTimeout = Defaults.Timeouts.ElementOperation;

    public ElementFinder(ILogger<ElementFinder> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IWebElement> FindElementAsync(
        AppiumDriver driver,
        Selector selector,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var by = GetByFromSelector(selector);

        _logger.LogDebug("Finding element with selector type {Type} and value {Value} (timeout: {Timeout})",
            GetSelectorType(selector), GetSelectorValue(selector), effectiveTimeout);

        try
        {
            var wait = new WebDriverWait(driver, effectiveTimeout);
            
            // Direct WebDriverWait usage without Task.Run wrapper
            var element = wait.Until(d =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var el = d.FindElement(by);
                    return el?.Displayed == true ? el : null;
                }
                catch (NoSuchElementException)
                {
                    return null;
                }
            });

            if (element == null)
            {
                throw new NoSuchElementException($"Element not found with selector: {GetSelectorDescription(selector)}");
            }

            _logger.LogDebug("Element found successfully");
            return element;
        }
        catch (WebDriverTimeoutException)
        {
            _logger.LogWarning("Timeout waiting for element with selector: {Selector}", GetSelectorDescription(selector));
            throw new TimeoutException($"Element not found within {effectiveTimeout}: {GetSelectorDescription(selector)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error finding element with selector: {Selector}", GetSelectorDescription(selector));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> WaitForElementAsync(
        AppiumDriver driver,
        Selector selector,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await FindElementAsync(driver, selector, timeout, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (NoSuchElementException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public By GetByFromSelector(Selector selector)
    {
        // Check each selector type and return appropriate By
        if (!string.IsNullOrEmpty(selector.AccessibilityId))
        {
            return MobileBy.AccessibilityId(selector.AccessibilityId);
        }

        if (!string.IsNullOrEmpty(selector.Id))
        {
            return By.Id(selector.Id);
        }

        if (!string.IsNullOrEmpty(selector.IosClassChain))
        {
            return MobileBy.IosClassChain(selector.IosClassChain);
        }

        if (!string.IsNullOrEmpty(selector.AndroidUiautomator))
        {
            return MobileBy.AndroidUIAutomator(selector.AndroidUiautomator);
        }

        if (!string.IsNullOrEmpty(selector.Xpath))
        {
            return By.XPath(selector.Xpath);
        }

        throw new ArgumentException("Selector must have at least one valid locator strategy defined");
    }

    private static string GetSelectorType(Selector selector)
    {
        if (!string.IsNullOrEmpty(selector.AccessibilityId)) return "AccessibilityId";
        if (!string.IsNullOrEmpty(selector.Id)) return "Id";
        if (!string.IsNullOrEmpty(selector.IosClassChain)) return "IosClassChain";
        if (!string.IsNullOrEmpty(selector.AndroidUiautomator)) return "AndroidUiautomator";
        if (!string.IsNullOrEmpty(selector.Xpath)) return "XPath";
        return "Unknown";
    }

    private static string GetSelectorValue(Selector selector)
    {
        if (!string.IsNullOrEmpty(selector.AccessibilityId)) return selector.AccessibilityId;
        if (!string.IsNullOrEmpty(selector.Id)) return selector.Id;
        if (!string.IsNullOrEmpty(selector.IosClassChain)) return selector.IosClassChain;
        if (!string.IsNullOrEmpty(selector.AndroidUiautomator)) return selector.AndroidUiautomator;
        if (!string.IsNullOrEmpty(selector.Xpath)) return selector.Xpath;
        return "N/A";
    }

    private static string GetSelectorDescription(Selector selector)
    {
        var type = GetSelectorType(selector);
        var value = GetSelectorValue(selector);
        return $"{type}: {value}";
    }
}