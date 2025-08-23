using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Snappium.Core.Abstractions;
using Snappium.Core.Infrastructure;
using Snappium.Core.Config;
using Snappium.Core.Orchestration;
using Snappium.Core.Planning;

namespace Snappium.Tests;

[TestFixture]
public class OrchestratorTests
{
    IServiceProvider _serviceProvider = null!;
    Mock<IJobExecutor> _jobExecutorMock = null!;
    Mock<IAppiumServerController> _appiumServerControllerMock = null!;
    Mock<ILogger<Orchestrator>> _loggerMock = null!;
    Orchestrator _orchestrator = null!;

    [SetUp]
    public void SetUp()
    {
        _jobExecutorMock = new Mock<IJobExecutor>();
        _appiumServerControllerMock = new Mock<IAppiumServerController>();
        _loggerMock = new Mock<ILogger<Orchestrator>>();

        // Create a real service collection with mocked services
        var services = new ServiceCollection();
        services.AddScoped<IJobExecutor>(_ => _jobExecutorMock.Object);
        _serviceProvider = services.BuildServiceProvider();

        _orchestrator = new Orchestrator(
            _serviceProvider,
            _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        (_serviceProvider as IDisposable)?.Dispose();
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

        // Mock job executor
        _jobExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<RunJob>(), It.IsAny<RootConfig>(), It.IsAny<CliOverrides>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JobResult
            {
                Job = job,
                Status = JobStatus.Success,
                StartTime = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndTime = DateTimeOffset.UtcNow,
                Screenshots = new List<ScreenshotResult>
                {
                    new ScreenshotResult
                    {
                        Name = "test",
                        Path = "/tmp/test.png",
                        Timestamp = DateTimeOffset.UtcNow,
                        Success = true
                    }
                },
                FailureArtifacts = new List<FailureArtifact>()
            });

        // Act
        var result = await _orchestrator.ExecuteAsync(runPlan, config);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.JobResults, Has.Count.EqualTo(1));
        Assert.That(result.JobResults[0].Success, Is.True);
        Assert.That(result.JobResults[0].Screenshots, Has.Count.EqualTo(1));
    }

    static RootConfig CreateMinimalConfig()
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
            },
            Artifacts = new Artifacts
            {
                Ios = new IosArtifact
                {
                    ArtifactGlob = "test.app",
                    Package = "com.test.app"
                },
                Android = new AndroidArtifact
                {
                    ArtifactGlob = "test.apk",
                    Package = "com.test.app"
                }
            }
        };
    }

    static RunJob CreateTestJob()
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