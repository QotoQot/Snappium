namespace Snappium.Core.Abstractions;

/// <summary>
/// Port allocation for a single job to avoid conflicts in parallel execution.
/// </summary>
/// <param name="AppiumPort">Main Appium server port</param>
/// <param name="WdaLocalPort">iOS WebDriverAgent local port</param>
/// <param name="SystemPort">Android UiAutomator2 system port</param>
public readonly record struct PortAllocation(
    int AppiumPort,
    int WdaLocalPort,
    int SystemPort
);