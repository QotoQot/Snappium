namespace Snappium.Core.Config;

/// <summary>
/// Timeout configuration for various operations.
/// </summary>
public sealed class Timeouts
{
    /// <summary>
    /// Default wait timeout in milliseconds (1000-60000).
    /// </summary>
    public int? DefaultWaitMs { get; init; }

    /// <summary>
    /// Implicit wait timeout in milliseconds (0-10000).
    /// </summary>
    public int? ImplicitWaitMs { get; init; }

    /// <summary>
    /// Page load timeout in milliseconds (10000-120000).
    /// </summary>
    public int? PageLoadTimeoutMs { get; init; }
}

/// <summary>
/// Port allocation configuration for parallel execution.
/// </summary>
public sealed class Ports
{
    /// <summary>
    /// Base port number for Appium server (1024-65535).
    /// </summary>
    public int? BasePort { get; init; }

    /// <summary>
    /// Port offset between parallel jobs (1-100).
    /// </summary>
    public int? PortOffset { get; init; }
}