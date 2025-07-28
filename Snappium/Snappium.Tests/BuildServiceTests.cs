using Microsoft.Extensions.Logging;
using Moq;
using Snappium.Core.Abstractions;
using Snappium.Core.Build;
using Snappium.Core.Infrastructure;

namespace Snappium.Tests;

[TestFixture]
public class BuildServiceTests
{
    private Mock<ICommandRunner> _commandRunnerMock = null!;
    private Mock<ILogger<BuildService>> _loggerMock = null!;
    private BuildService _buildService = null!;
    private string _tempProjectPath = null!;

    [SetUp]
    public void SetUp()
    {
        _commandRunnerMock = new Mock<ICommandRunner>();
        _loggerMock = new Mock<ILogger<BuildService>>();
        _buildService = new BuildService(_commandRunnerMock.Object, _loggerMock.Object);
        
        // Create a temporary project file for testing
        _tempProjectPath = Path.GetTempFileName();
        File.WriteAllText(_tempProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_tempProjectPath))
        {
            File.Delete(_tempProjectPath);
        }
    }

    [Test]
    public async Task BuildAsync_ProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = "/path/to/nonexistent.csproj";

        // Act
        var result = await _buildService.BuildAsync(Platform.iOS, nonExistentPath);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Project file not found"));
    }

    [Test]
    public async Task BuildAsync_BuildSucceeds_ReturnsSuccess()
    {
        // Arrange
        _commandRunnerMock
            .Setup(x => x.RunAsync("dotnet", It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, StandardOutput = "Build succeeded", StandardError = "", ExecutionTime = TimeSpan.FromSeconds(1) });

        // Act
        var result = await _buildService.BuildAsync(Platform.iOS, _tempProjectPath);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
        Assert.That(result.Duration, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public async Task BuildAsync_BuildFails_ReturnsFailure()
    {
        // Arrange
        _commandRunnerMock
            .Setup(x => x.RunAsync("dotnet", It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 1, StandardOutput = "", StandardError = "Build failed", ExecutionTime = TimeSpan.FromSeconds(1) });

        // Act
        var result = await _buildService.BuildAsync(Platform.Android, _tempProjectPath);

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Build failed"));
        Assert.That(result.Duration, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public async Task BuildAsync_iOSPlatform_IncludesRuntimeIdentifier()
    {
        // Arrange
        _commandRunnerMock
            .Setup(x => x.RunAsync("dotnet", It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, StandardOutput = "Build succeeded", StandardError = "", ExecutionTime = TimeSpan.FromSeconds(1) });

        // Act
        await _buildService.BuildAsync(Platform.iOS, _tempProjectPath, "Release");

        // Assert - Should be called twice: once for build, once for getting output path
        _commandRunnerMock.Verify(x => x.RunAsync(
            "dotnet",
            It.Is<string[]>(args => args.Contains("build") && args.Contains("-p:RuntimeIdentifier=ios-arm64")),
            It.IsAny<string?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once, "Should call dotnet build with iOS runtime identifier");
            
        _commandRunnerMock.Verify(x => x.RunAsync(
            "dotnet",
            It.Is<string[]>(args => args.Contains("msbuild") && args.Contains("-getProperty:OutputPath") && args.Contains("-p:RuntimeIdentifier=ios-arm64")),
            It.IsAny<string?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once, "Should call dotnet msbuild to get output path with iOS runtime identifier");
    }

    [Test]
    public async Task BuildAsync_AndroidPlatform_IncludesAaptProperty()
    {
        // Arrange
        _commandRunnerMock
            .Setup(x => x.RunAsync("dotnet", It.IsAny<string[]>(), It.IsAny<string?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, StandardOutput = "Build succeeded", StandardError = "", ExecutionTime = TimeSpan.FromSeconds(1) });

        // Act
        await _buildService.BuildAsync(Platform.Android, _tempProjectPath, "Debug", "net8.0-android");

        // Assert - Should be called twice: once for build, once for getting output path
        _commandRunnerMock.Verify(x => x.RunAsync(
            "dotnet",
            It.Is<string[]>(args => 
                args.Contains("build") &&
                args.Contains("-p:AndroidUseAapt2=true") &&
                args.Contains("-f") &&
                args.Contains("net8.0-android")),
            It.IsAny<string?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once, "Should call dotnet build with Android properties");
            
        _commandRunnerMock.Verify(x => x.RunAsync(
            "dotnet",
            It.Is<string[]>(args => 
                args.Contains("msbuild") &&
                args.Contains("-getProperty:OutputPath") &&
                args.Contains("-p:AndroidUseAapt2=true") &&
                args.Contains("-p:TargetFramework=net8.0-android")),
            It.IsAny<string?>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once, "Should call dotnet msbuild to get output path with Android properties");
    }

    [Test]
    public async Task DiscoverArtifactAsync_NoFiles_ReturnsNull()
    {
        // Arrange
        var tempDir = Path.GetTempPath();

        // Act
        var result = await _buildService.DiscoverArtifactAsync("*.nonexistent", tempDir);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task DiscoverArtifactAsync_MultipleFiles_ReturnsLatest()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        var file1 = Path.Combine(tempDir, "test1.dll");
        var file2 = Path.Combine(tempDir, "test2.dll");
        
        File.WriteAllText(file1, "test");
        await Task.Delay(100); // Ensure different timestamps
        File.WriteAllText(file2, "test");

        try
        {
            // Act
            var result = await _buildService.DiscoverArtifactAsync("test*.dll", tempDir);

            // Assert
            Assert.That(result, Is.EqualTo(file2));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}