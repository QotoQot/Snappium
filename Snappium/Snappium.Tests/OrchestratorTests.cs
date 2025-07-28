using Microsoft.Extensions.Logging;
using Moq;
using OpenQA.Selenium.Appium;
using Snappium.Core.Abstractions;
using Snappium.Core.Appium;
using Snappium.Core.Build;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Snappium.Core.Logging;
using Snappium.Core.Orchestration;
using Snappium.Core.Planning;

namespace Snappium.Tests;

[TestFixture]
public class OrchestratorTests
{
    private Mock<IDriverFactory> _driverFactoryMock = null!;
    private Mock<IActionExecutor> _actionExecutorMock = null!;
    private Mock<IImageValidator> _imageValidatorMock = null!;
    private Mock<IIosDeviceManager> _iosDeviceManagerMock = null!;
    private Mock<IAndroidDeviceManager> _androidDeviceManagerMock = null!;
    private Mock<IBuildService> _buildServiceMock = null!;
    private Mock<IAppiumServerController> _appiumServerControllerMock = null!;
    private Mock<ILogger<Orchestrator>> _loggerMock = null!;
    private Mock<ISnappiumLogger> _snappiumLoggerMock = null!;
    private PortAllocator _portAllocator = null!;
    private Orchestrator _orchestrator = null!;

    [SetUp]
    public void SetUp()
    {
        _driverFactoryMock = new Mock<IDriverFactory>();
        _actionExecutorMock = new Mock<IActionExecutor>();
        _imageValidatorMock = new Mock<IImageValidator>();
        _iosDeviceManagerMock = new Mock<IIosDeviceManager>();
        _androidDeviceManagerMock = new Mock<IAndroidDeviceManager>();
        _buildServiceMock = new Mock<IBuildService>();
        _appiumServerControllerMock = new Mock<IAppiumServerController>();
        _loggerMock = new Mock<ILogger<Orchestrator>>();
        _snappiumLoggerMock = new Mock<ISnappiumLogger>();
        _portAllocator = new PortAllocator(4723, 10);

        _orchestrator = new Orchestrator(
            _driverFactoryMock.Object,
            _actionExecutorMock.Object,
            _imageValidatorMock.Object,
            _iosDeviceManagerMock.Object,
            _androidDeviceManagerMock.Object,
            _buildServiceMock.Object,
            _appiumServerControllerMock.Object,
            _portAllocator,
            _loggerMock.Object,
            _snappiumLoggerMock.Object);
    }

    [Test]
    public async Task ExecuteAsync_EmptyRunPlan_ReturnsSuccessfulResult()
    {
        // Arrange
        var runPlan = new RunPlan
        {
            Jobs = new List<RunJob>(),
            TotalPlatforms = 0,
            TotalDevices = 0,
            TotalLanguages = 0,
            TotalScreenshots = 0,
            EstimatedDurationMinutes = 0.0,
            ArtifactPaths = new Dictionary<Platform, string>()
        };
        var config = CreateMinimalConfig();

        // Act
        var result = await _orchestrator.ExecuteAsync(runPlan, config);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.JobResults, Is.Empty);
        Assert.That(result.RunId, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Environment.OperatingSystem, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task ExecuteAsync_WithJobs_CreatesJobResults()
    {
        // Arrange
        var job = CreateTestJob();
        var runPlan = new RunPlan
        {
            Jobs = new List<RunJob> { job },
            TotalPlatforms = 1,
            TotalDevices = 1,
            TotalLanguages = 1,
            TotalScreenshots = 1,
            EstimatedDurationMinutes = 2.0,
            ArtifactPaths = new Dictionary<Platform, string> { [Platform.iOS] = "/tmp/test.app" }
        };
        var config = CreateMinimalConfig();

        // Mock device manager calls
        _iosDeviceManagerMock
            .Setup(x => x.ShutdownAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _iosDeviceManagerMock
            .Setup(x => x.SetLanguageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<LocaleMapping>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _iosDeviceManagerMock
            .Setup(x => x.BootAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _iosDeviceManagerMock
            .Setup(x => x.InstallAppAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock driver creation - return null since we're not actually using the driver in this test
        _driverFactoryMock
            .Setup(x => x.CreateDriverAsync(It.IsAny<RunJob>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppiumDriver)null!);

        // Mock action executor
        _actionExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<AppiumDriver>(), It.IsAny<RunJob>(), It.IsAny<string>(),
                It.IsAny<ScreenshotPlan>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ScreenshotResult>
            {
                new ScreenshotResult
                {
                    Name = "test",
                    Path = "/tmp/test.png",
                    Timestamp = DateTimeOffset.UtcNow,
                    Success = true
                }
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(runPlan, config);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.JobResults, Has.Count.EqualTo(1));
        Assert.That(result.JobResults[0].Success, Is.True);
        Assert.That(result.JobResults[0].Screenshots, Has.Count.EqualTo(1));
    }

    private static RootConfig CreateMinimalConfig()
    {
        return new RootConfig
        {
            Devices = new Devices
            {
                Ios = new List<IosDevice>
                {
                    new IosDevice
                    {
                        Name = "iPhone 15",
                        Udid = "test-udid",
                        Folder = "iphone15",
                        PlatformVersion = "17.0"
                    }
                },
                Android = new List<AndroidDevice>()
            },
            Languages = new List<string> { "en-US" },
            LocaleMapping = new Dictionary<string, LocaleMapping>
            {
                ["en-US"] = new LocaleMapping
                {
                    Ios = "en_US",
                    Android = "en_US"
                }
            },
            Screenshots = new List<ScreenshotPlan>
            {
                new ScreenshotPlan
                {
                    Name = "home",
                    Actions = new List<ScreenshotAction>
                    {
                        new ScreenshotAction
                        {
                            Capture = new CaptureConfig { Name = "home" }
                        }
                    }
                }
            }
        };
    }

    private static RunJob CreateTestJob()
    {
        return new RunJob
        {
            Platform = Platform.iOS,
            IosDevice = new IosDevice
            {
                Name = "iPhone 15",
                Udid = "test-udid",
                Folder = "iphone15",
                PlatformVersion = "17.0"
            },
            Language = "en-US",
            LocaleMapping = new LocaleMapping
            {
                Ios = "en_US",
                Android = "en_US"
            },
            OutputDirectory = "/tmp/screenshots",
            Screenshots = new List<ScreenshotPlan>
            {
                new ScreenshotPlan
                {
                    Name = "home",
                    Actions = new List<ScreenshotAction>
                    {
                        new ScreenshotAction
                        {
                            Capture = new CaptureConfig { Name = "home" }
                        }
                    }
                }
            },
            Ports = new PortAllocation(4723, 4724, 4725),
            AppPath = "/tmp/test.app"
        };
    }
}