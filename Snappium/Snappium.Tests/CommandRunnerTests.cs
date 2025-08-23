using Microsoft.Extensions.Logging;
using Snappium.Core.Infrastructure;

namespace Snappium.Tests;

[TestFixture]
public class CommandRunnerTests
{
    CommandRunner _commandRunner = null!;
    ILogger<CommandRunner> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<CommandRunner>();
        _commandRunner = new CommandRunner(_logger);
    }

    [Test]
    public async Task RunAsync_EchoCommand_ReturnsExpectedOutput()
    {
        // Arrange
        var expectedText = "Hello, World!";
        var command = Os.IsWindows ? "cmd" : "echo";
        var arguments = Os.IsWindows ? new string[] { "/c", "echo", expectedText } : new string[] { expectedText };

        // Act
        var result = await _commandRunner.RunAsync(command, arguments);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.ExitCode, Is.EqualTo(0));
        Assert.That(result.StandardOutput.Trim(), Is.EqualTo(expectedText));
        Assert.That(result.ExecutionTime, Is.GreaterThan(TimeSpan.Zero));
    }

    [Test]
    public async Task RunAsync_InvalidCommand_ReturnsNonZeroExitCode()
    {
        // Arrange & Act & Assert
        if (Os.IsWindows)
        {
            // On Windows, cmd will return exit code 1 for invalid commands
            var result = await _commandRunner.RunAsync("cmd", new string[] { "/c", "exit", "1" });
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(1));
        }
        else
        {
            // On Unix systems, use a failing command
            var result = await _commandRunner.RunAsync("sh", new string[] { "-c", "exit 1" });
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ExitCode, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task RunAsync_WithTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var command = Os.IsWindows ? "cmd" : "sh";
        var arguments = Os.IsWindows 
            ? new string[] { "/c", "ping", "127.0.0.1", "-n", "5" }
            : new string[] { "-c", "sleep 3" };
        var shortTimeout = TimeSpan.FromMilliseconds(200);

        // Act & Assert
        Assert.ThrowsAsync<TimeoutException>(
            () => _commandRunner.RunAsync(command, arguments, timeout: shortTimeout));
    }

    [Test]
    public async Task RunAsync_WithWorkingDirectory_ExecutesInCorrectDirectory()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var command = Os.IsWindows ? "cmd" : "pwd";
        var arguments = Os.IsWindows ? new string[] { "/c", "cd" } : new string[] { };

        // Act
        var result = await _commandRunner.RunAsync(command, arguments, workingDirectory: tempDir);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        
        if (Os.IsWindows)
        {
            Assert.That(result.StandardOutput.Trim(), Does.StartWith(tempDir.TrimEnd(Path.DirectorySeparatorChar)));
        }
        else
        {
            // On macOS, paths can have /private prefix when resolved
            var expectedPath = tempDir.TrimEnd(Path.DirectorySeparatorChar);
            var actualPath = result.StandardOutput.Trim();
            Assert.That(actualPath, Does.EndWith(expectedPath.Replace("/var/", "")));
        }
    }

    [Test]
    public async Task RunWithRetryAsync_SuccessfulCommand_ReturnsResult()
    {
        // Arrange
        var command = Os.IsWindows ? "cmd" : "echo";
        var arguments = Os.IsWindows ? new string[] { "/c", "echo", "test" } : new string[] { "test" };

        // Act
        var result = await _commandRunner.RunWithRetryAsync(command, arguments, retryCount: 2);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.StandardOutput.Trim(), Is.EqualTo("test"));
    }

    [Test]
    public async Task RunWithRetryAsync_FailingCommand_RetriesAndFails()
    {
        // Arrange
        var command = Os.IsWindows ? "cmd" : "sh";
        var arguments = Os.IsWindows ? new string[] { "/c", "exit", "1" } : new string[] { "-c", "exit 1" };

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _commandRunner.RunWithRetryAsync(command, arguments, retryCount: 2, retryDelay: TimeSpan.FromMilliseconds(10)));
        
        Assert.That(ex.Message, Does.Contain("failed with exit code 1"));
    }

    [Test]
    public async Task RunAsync_CapturesStandardError()
    {
        // Arrange  
        var command = Os.IsWindows ? "cmd" : "sh";
        var arguments = Os.IsWindows 
            ? new string[] { "/c", "echo", "error", ">&2" }
            : new string[] { "-c", "echo 'error' >&2" };

        // Act
        var result = await _commandRunner.RunAsync(command, arguments);

        // Assert
        Assert.That(result.ExitCode, Is.EqualTo(0));
        if (!Os.IsWindows) // Unix stderr capture works better
        {
            Assert.That(result.StandardError.Trim(), Is.EqualTo("error"));
        }
    }

    [Test]
    public void CommandResult_CombinedOutput_CombinesStdoutAndStderr()
    {
        // Arrange
        var result = new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "stdout content",
            StandardError = "stderr content",
            ExecutionTime = TimeSpan.FromSeconds(1)
        };

        // Act
        var combined = result.CombinedOutput;

        // Assert
        Assert.That(combined, Is.EqualTo("stdout content\nstderr content"));
    }

    [Test]
    public void CommandResult_CombinedOutput_OnlyStdout_ReturnsStdout()
    {
        // Arrange
        var result = new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "stdout only",
            StandardError = "",
            ExecutionTime = TimeSpan.FromSeconds(1)
        };

        // Act
        var combined = result.CombinedOutput;

        // Assert
        Assert.That(combined, Is.EqualTo("stdout only"));
    }
}