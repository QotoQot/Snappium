using Microsoft.Extensions.Logging;

namespace Snappium.Core.Infrastructure;

/// <summary>
/// Managed wrapper for Appium server instances that can be tracked and cleaned up.
/// </summary>
public sealed class ManagedAppiumServer : IManagedProcess
{
    readonly IAppiumServerController _appiumServerController;
    readonly int _port;
    readonly ILogger<ManagedAppiumServer> _logger;

    public ManagedAppiumServer(
        IAppiumServerController appiumServerController,
        int port,
        ILogger<ManagedAppiumServer> logger)
    {
        _appiumServerController = appiumServerController;
        _port = port;
        _logger = logger;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Stopping managed Appium server on port {Port}", _port);
            var result = await _appiumServerController.StopServerAsync(_port, cancellationToken);
            
            if (result.Success)
            {
                _logger.LogDebug("Successfully stopped Appium server on port {Port}", _port);
            }
            else
            {
                _logger.LogWarning("Failed to stop Appium server on port {Port}: {Error}", _port, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while stopping Appium server on port {Port}", _port);
        }
    }
}