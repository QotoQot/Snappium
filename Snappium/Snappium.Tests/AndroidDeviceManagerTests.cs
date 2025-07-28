using Microsoft.Extensions.Logging;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Snappium.Core.Infrastructure;

namespace Snappium.Tests;

[TestFixture]
public class AndroidDeviceManagerTests
{
    private AndroidDeviceManager _deviceManager = null!;
    private TestCommandRunner _commandRunner = null!;
    private ILogger<AndroidDeviceManager> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<AndroidDeviceManager>();
        _commandRunner = new TestCommandRunner();
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

    // [Test]
    // public async Task SetStatusBarDemoModeAsync_ValidConfiguration_ExecutesCorrectCommands()
    // {
    //     // Arrange
    //     _commandRunner.SetupDefaultAndroidEmulator();
    //     var deviceSerial = await _deviceManager.StartEmulatorAsync("test-avd");
    //
    //     var statusBar = new AndroidStatusBar
    //     {
    //         Clock = "1234",
    //         Battery = 100,
    //         Wifi = "4"
    //     };
    //
    //     _commandRunner.SetupCommand("adb", args => args.Contains("settings"), exitCode: 0);
    //     _commandRunner.SetupCommand("adb", args => args.Contains("broadcast"), exitCode: 0);
    //
    //     // Act
    //     await _deviceManager.SetStatusBarDemoModeAsync("emulator-5554", statusBar);
    //
    //     // Assert
    //     var broadcastCommands = _commandRunner.ExecutedCommands
    //         .Where(c => c.Command == "adb" && c.Arguments.Contains("broadcast"))
    //         .ToList();
    //
    //     Assert.That(broadcastCommands.Count, Is.GreaterThan(0));
    // }

    [Test]
    public void StopEmulatorAsync_NoActiveEmulator_LogsInformationAndCompletes()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(() => _deviceManager.StopEmulatorAsync("emulator-5554"));
    }

    // Helper class to mock ICommandRunner for testing
    private class TestCommandRunner : ICommandRunner
    {
        public List<ExecutedCommand> ExecutedCommands { get; } = new();
        private readonly Dictionary<string, CommandResult> _setupCommands = new();

        public void SetupCommand(string command, string[] arguments, int exitCode = 0, string stdout = "", string stderr = "")
        {
            var key = $"{command}|{string.Join("|", arguments)}";
            _setupCommands[key] = new CommandResult
            {
                ExitCode = exitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                ExecutionTime = TimeSpan.FromMilliseconds(100)
            };
        }

        public void SetupCommand(string command, Func<string[], bool> argumentMatcher, int exitCode = 0, string stdout = "", string stderr = "")
        {
            // Store with a special marker for matcher-based commands
            var existingMatchers = _setupCommands.Keys.Count(k => k.StartsWith($"{command}|matcher"));
            var key = $"{command}|matcher|{existingMatchers}";
            _setupCommands[key] = new CommandResult
            {
                ExitCode = exitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                ExecutionTime = TimeSpan.FromMilliseconds(100)
            };
        }

        public void SetupDefaultAndroidEmulator()
        {
            // Setup for adb devices command to show emulator
            SetupCommand("adb", ["devices"], exitCode: 0, stdout: "emulator-5554\tdevice\n");
            
            // Setup for emulator command
            SetupCommand("emulator", args => args.Contains("-avd"), exitCode: 0);
            
            // Setup for boot completion check
            SetupCommand("adb", args => args.Contains("getprop") && args.Contains("sys.boot_completed"), exitCode: 0, stdout: "1");
        }

        public Task<CommandResult> RunAsync(string command, string[]? arguments = null, string? workingDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            arguments ??= [];
            ExecutedCommands.Add(new ExecutedCommand(command, arguments, workingDirectory));

            var key = $"{command}|{string.Join("|", arguments)}";
            if (_setupCommands.TryGetValue(key, out var result))
            {
                return Task.FromResult(result);
            }

            // Try matcher-based setups
            foreach (var kvp in _setupCommands.Where(kv => kv.Key.StartsWith($"{command}|matcher")))
            {
                // For simplicity, just return the result - in real implementation you'd check the matcher
                return Task.FromResult(kvp.Value);
            }

            // Default success response
            return Task.FromResult(new CommandResult
            {
                ExitCode = 0,
                StandardOutput = "",
                StandardError = "",
                ExecutionTime = TimeSpan.FromMilliseconds(100)
            });
        }

        public Task<CommandResult> RunWithRetryAsync(string command, string[]? arguments = null, string? workingDirectory = null, TimeSpan? timeout = null, int retryCount = 3, TimeSpan? retryDelay = null, CancellationToken cancellationToken = default)
        {
            return RunAsync(command, arguments, workingDirectory, timeout, cancellationToken);
        }
    }

    public record ExecutedCommand(string Command, string[] Arguments, string? WorkingDirectory);
}