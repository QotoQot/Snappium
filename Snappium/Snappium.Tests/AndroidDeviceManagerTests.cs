using Microsoft.Extensions.Logging;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Snappium.Tests.TestHelpers;

namespace Snappium.Tests;

[TestFixture]
public class AndroidDeviceManagerTests
{
    AndroidDeviceManager _deviceManager = null!;
    MockCommandRunner _commandRunner = null!;
    ILogger<AndroidDeviceManager> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<AndroidDeviceManager>();
        _commandRunner = new MockCommandRunner();
        _deviceManager = new AndroidDeviceManager(_commandRunner, _logger);
    }

    [Test]
    public async Task GetCapabilities_ValidDevice_ReturnsCorrectCapabilities()
    {
        // Arrange
        var device = new AndroidDevice
        {
            Name = "Pixel 7 Pro",
            Avd = "Pixel_7_Pro_API_34",
            Folder = "Phone_6_7",
            PlatformVersion = "14"
        };
        var localeMapping = new LocaleMapping { Ios = "en_US", Android = "en_US" };
        var apkPath = "/path/to/app.apk";

        // Act
        var capabilities = _deviceManager.GetCapabilities(device, "en-US", localeMapping, apkPath);

        // Assert
        Assert.That(capabilities["platformName"], Is.EqualTo("Android"));
        Assert.That(capabilities["platformVersion"], Is.EqualTo("14"));
        Assert.That(capabilities["deviceName"], Is.EqualTo("Pixel 7 Pro"));
        Assert.That(capabilities["avd"], Is.EqualTo("Pixel_7_Pro_API_34"));
        Assert.That(capabilities["app"], Is.EqualTo(apkPath));
        Assert.That(capabilities["language"], Is.EqualTo("en_US"));
        Assert.That(capabilities["locale"], Is.EqualTo("en_US"));
    }

    [Test]
    public void StopEmulatorAsync_NoActiveEmulator_LogsInformationAndCompletes()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(() => _deviceManager.StopEmulatorAsync("emulator-5554"));
    }

    // Helper class to mock ICommandRunner for testing
}