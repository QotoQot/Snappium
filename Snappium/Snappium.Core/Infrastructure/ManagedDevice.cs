using Microsoft.Extensions.Logging;
using Snappium.Core.Abstractions;
using Snappium.Core.DeviceManagement;

namespace Snappium.Core.Infrastructure;

/// <summary>
/// Managed wrapper for iOS simulator instances.
/// </summary>
public sealed class ManagedIosSimulator : IManagedProcess
{
    private readonly IIosDeviceManager _iosDeviceManager;
    private readonly string _udid;
    private readonly ILogger<ManagedIosSimulator> _logger;

    public ManagedIosSimulator(
        IIosDeviceManager iosDeviceManager,
        string udid,
        ILogger<ManagedIosSimulator> logger)
    {
        _iosDeviceManager = iosDeviceManager;
        _udid = udid;
        _logger = logger;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Shutting down managed iOS simulator: {Udid}", _udid);
            await _iosDeviceManager.ShutdownAsync(_udid, cancellationToken);
            _logger.LogDebug("Successfully shut down iOS simulator: {Udid}", _udid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while shutting down iOS simulator: {Udid}", _udid);
        }
    }
}

/// <summary>
/// Managed wrapper for Android emulator instances.
/// </summary>
public sealed class ManagedAndroidEmulator : IManagedProcess
{
    private readonly IAndroidDeviceManager _androidDeviceManager;
    private readonly string _deviceSerial;
    private readonly ILogger<ManagedAndroidEmulator> _logger;

    public ManagedAndroidEmulator(
        IAndroidDeviceManager androidDeviceManager,
        string deviceSerial,
        ILogger<ManagedAndroidEmulator> logger)
    {
        _androidDeviceManager = androidDeviceManager;
        _deviceSerial = deviceSerial;
        _logger = logger;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Stopping managed Android emulator: {DeviceSerial}", _deviceSerial);
            await _androidDeviceManager.StopEmulatorAsync(_deviceSerial, cancellationToken);
            _logger.LogDebug("Successfully stopped Android emulator: {DeviceSerial}", _deviceSerial);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while stopping Android emulator: {DeviceSerial}", _deviceSerial);
        }
    }
}