using Microsoft.Extensions.Logging;
using Moq;
using Snappium.Core.Abstractions;
using Snappium.Core.Config;
using Snappium.Core.Orchestration;
using Snappium.Core.Planning;

namespace Snappium.Tests;

[TestFixture]
public class ManifestWriterTests
{
    private Mock<ILogger<ManifestWriter>> _loggerMock = null!;
    private ManifestWriter _manifestWriter = null!;
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _loggerMock = new Mock<ILogger<ManifestWriter>>();
        _manifestWriter = new ManifestWriter(_loggerMock.Object);
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Test]
    public async Task WriteAsync_CreatesManifestAndSummaryFiles()
    {
        // Arrange
        var runResult = CreateTestRunResult();

        // Act
        var result = await _manifestWriter.WriteAsync(runResult, _tempDirectory);

        // Assert
        Assert.That(File.Exists(result.ManifestJsonPath), Is.True);
        Assert.That(File.Exists(result.SummaryTextPath), Is.True);
        Assert.That(result.ManifestJsonPath, Does.EndWith("run_manifest.json"));
        Assert.That(result.SummaryTextPath, Does.EndWith("run_summary.txt"));
    }

    [Test]
    public async Task WriteAsync_ManifestContainsExpectedFields()
    {
        // Arrange
        var runResult = CreateTestRunResult();

        // Act
        var result = await _manifestWriter.WriteAsync(runResult, _tempDirectory);
        var manifestContent = await File.ReadAllTextAsync(result.ManifestJsonPath);

        // Assert
        Assert.That(manifestContent, Does.Contain("run_id"));
        Assert.That(manifestContent, Does.Contain("start_time"));
        Assert.That(manifestContent, Does.Contain("end_time"));
        Assert.That(manifestContent, Does.Contain("success"));
        Assert.That(manifestContent, Does.Contain("environment"));
        Assert.That(manifestContent, Does.Contain("jobs"));
        Assert.That(manifestContent, Does.Contain("summary"));
    }

    [Test]
    public async Task WriteAsync_SummaryContainsReadableText()
    {
        // Arrange
        var runResult = CreateTestRunResult();

        // Act
        var result = await _manifestWriter.WriteAsync(runResult, _tempDirectory);
        var summaryContent = await File.ReadAllTextAsync(result.SummaryTextPath);

        // Assert
        Assert.That(summaryContent, Does.Contain("Snappium Screenshot Run Summary"));
        Assert.That(summaryContent, Does.Contain("Run ID:"));
        Assert.That(summaryContent, Does.Contain("Environment:"));
        Assert.That(summaryContent, Does.Contain("Summary:"));
        Assert.That(summaryContent, Does.Contain("Job Results:"));
    }

    private static RunResult CreateTestRunResult()
    {
        var startTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var endTime = DateTimeOffset.UtcNow;

        return new RunResult
        {
            RunId = "test123",
            StartTime = startTime,
            EndTime = endTime,
            Success = true,
            JobResults = new List<JobResult>
            {
                new JobResult
                {
                    Job = new RunJob
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
                        LocaleMapping = new LocaleMapping { Ios = "en_US", Android = "en_US" },
                        OutputDirectory = "/tmp/test",
                        Screenshots = new List<ScreenshotPlan>(),
                        Ports = new PortAllocation(4723, 4724, 4725),
                        AppPath = "/tmp/test.app"
                    },
                    Status = JobStatus.Success,
                    StartTime = startTime,
                    EndTime = endTime,
                    Screenshots = new List<ScreenshotResult>
                    {
                        new ScreenshotResult
                        {
                            Name = "home",
                            Path = "/tmp/home.png",
                            Timestamp = DateTimeOffset.UtcNow,
                            Success = true
                        }
                    },
                    FailureArtifacts = new List<FailureArtifact>()
                }
            },
            Environment = new Snappium.Core.Orchestration.EnvironmentInfo
            {
                OperatingSystem = "Test OS",
                DotNetVersion = ".NET 9.0",
                Hostname = "test-host",
                WorkingDirectory = "/tmp",
                SnappiumVersion = "1.0.0"
            }
        };
    }
}