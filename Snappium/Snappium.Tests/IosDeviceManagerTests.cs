using Microsoft.Extensions.Logging;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Snappium.Core.Infrastructure;
using Snappium.Tests.TestHelpers;
using System.Text.Json;

namespace Snappium.Tests;

[TestFixture]
public class IosDeviceManagerTests
{
    private IosDeviceManager _deviceManager = null!;
    private MockCommandRunner _commandRunner = null!;
    private ILogger<IosDeviceManager> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<IosDeviceManager>();
        _commandRunner = new MockCommandRunner();
        _deviceManager = new IosDeviceManager(_commandRunner, _logger);
    }

    [Test]
    public async Task GetCapabilities_ValidDevice_ReturnsCorrectCapabilities()
    {
        // Arrange
        var device = new IosDevice
        {
            Name = "iPhone 15 Pro",
            Udid = "test-udid",
            Folder = "iPhone_15_Pro",
            PlatformVersion = "17.0"
        };
        var localeMapping = new LocaleMapping { Ios = "en_US", Android = "en_US" };
        var appPath = "/path/to/app.app";

        // Act
        var capabilities = _deviceManager.GetCapabilities(device, "en-US", localeMapping, appPath);

        // Assert
        Assert.That(capabilities["platformName"], Is.EqualTo("iOS"));
        Assert.That(capabilities["platformVersion"], Is.EqualTo("17.0"));
        Assert.That(capabilities["deviceName"], Is.EqualTo("iPhone 15 Pro"));
        Assert.That(capabilities["udid"], Is.EqualTo("test-udid"));
        Assert.That(capabilities["app"], Is.EqualTo(appPath));
        Assert.That(capabilities["language"], Is.EqualTo("en_US"));
        Assert.That(capabilities["locale"], Is.EqualTo("en_US"));
    }

    [Test]
    public async Task SetStatusBarAsync_ValidConfiguration_ExecutesCorrectCommand()
    {
        // Arrange
        var deviceId = "test-device";
        var statusBar = new IosStatusBar
        {
            Time = "12:34",
            WifiBars = 3,
            BatteryState = "charged"
        };

        _commandRunner.SetupCommand("xcrun", args => args[0] == "simctl" && args[1] == "status_bar", exitCode: 0);

        // Act
        await _deviceManager.SetStatusBarAsync(deviceId, statusBar);

        // Assert
        Assert.That(_commandRunner.ExecutedCommands, Has.Count.EqualTo(1));
        var command = _commandRunner.ExecutedCommands[0];
        Assert.That(command.Arguments, Does.Contain("--time"));
        Assert.That(command.Arguments, Does.Contain("12:34"));
        Assert.That(command.Arguments, Does.Contain("--wifiBars"));
        Assert.That(command.Arguments, Does.Contain("3"));
    }

    // Helper class to mock ICommandRunner for testing
}