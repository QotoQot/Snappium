using Snappium.Core.Infrastructure;

namespace Snappium.Tests.TestHelpers;

/// <summary>
/// Mock implementation of ICommandRunner for testing purposes.
/// Provides extensive capabilities for setting up command expectations and verifying execution.
/// </summary>
public class MockCommandRunner : ICommandRunner
{
    private readonly List<ExecutedCommand> _executedCommands = new();
    private readonly Dictionary<string, CommandSetup> _commandSetups = new();
    private readonly List<CommandMatcher> _matcherSetups = new();

    /// <summary>
    /// List of all commands that have been executed.
    /// </summary>
    public IReadOnlyList<ExecutedCommand> ExecutedCommands => _executedCommands.AsReadOnly();

    /// <summary>
    /// Sets up a command with exact argument matching.
    /// </summary>
    /// <param name="command">The command to set up</param>
    /// <param name="arguments">The exact arguments to match</param>
    /// <param name="exitCode">Exit code to return (default: 0)</param>
    /// <param name="stdout">Standard output to return</param>
    /// <param name="stderr">Standard error to return</param>
    /// <param name="executionTime">Simulated execution time</param>
    public void SetupCommand(
        string command,
        string[] arguments,
        int exitCode = 0,
        string stdout = "",
        string stderr = "",
        TimeSpan? executionTime = null)
    {
        var key = CreateCommandKey(command, arguments);
        _commandSetups[key] = new CommandSetup
        {
            ExitCode = exitCode,
            StandardOutput = stdout,
            StandardError = stderr,
            ExecutionTime = executionTime ?? TimeSpan.FromMilliseconds(100)
        };
    }

    /// <summary>
    /// Sets up a command with custom argument matching logic.
    /// </summary>
    /// <param name="command">The command to set up</param>
    /// <param name="argumentMatcher">Function to match arguments</param>
    /// <param name="exitCode">Exit code to return (default: 0)</param>
    /// <param name="stdout">Standard output to return</param>
    /// <param name="stderr">Standard error to return</param>
    /// <param name="executionTime">Simulated execution time</param>
    public void SetupCommand(
        string command,
        Func<string[], bool> argumentMatcher,
        int exitCode = 0,
        string stdout = "",
        string stderr = "",
        TimeSpan? executionTime = null)
    {
        _matcherSetups.Add(new CommandMatcher
        {
            Command = command,
            ArgumentMatcher = argumentMatcher,
            Setup = new CommandSetup
            {
                ExitCode = exitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                ExecutionTime = executionTime ?? TimeSpan.FromMilliseconds(100)
            }
        });
    }

    /// <summary>
    /// Sets up common Android emulator commands for testing.
    /// </summary>
    public void SetupDefaultAndroidCommands()
    {
        // adb devices command
        SetupCommand("adb", ["devices"], exitCode: 0, stdout: "emulator-5554\tdevice\n");
        
        // Emulator start command (matches any -avd argument)
        SetupCommand("emulator", args => args.Contains("-avd"), exitCode: 0);
        
        // Boot completion check
        SetupCommand("adb", args => args.Contains("getprop") && args.Contains("sys.boot_completed"), 
            exitCode: 0, stdout: "1");
        
        // Language setting commands
        SetupCommand("adb", args => args.Contains("shell") && args.Contains("setprop"), exitCode: 0);
        
        // App installation
        SetupCommand("adb", args => args.Contains("install"), exitCode: 0);
        
        // Screenshot capture
        SetupCommand("adb", args => args.Contains("exec-out") && args.Contains("screencap"), exitCode: 0);
    }

    /// <summary>
    /// Sets up common iOS simulator commands for testing.
    /// </summary>
    public void SetupDefaultIosCommands()
    {
        // xcrun simctl list devices
        SetupCommand("xcrun", ["simctl", "list", "devices", "--json"], 
            exitCode: 0, stdout: """{"devices":{"iOS 17.2":[]}}""");
        
        // Simulator boot
        SetupCommand("xcrun", args => args.Contains("simctl") && args.Contains("boot"), exitCode: 0);
        
        // Simulator shutdown
        SetupCommand("xcrun", args => args.Contains("simctl") && args.Contains("shutdown"), exitCode: 0);
        
        // Language setting
        SetupCommand("xcrun", args => args.Contains("simctl") && args.Contains("spawn") && args.Contains("defaults"), exitCode: 0);
        
        // App installation
        SetupCommand("xcrun", args => args.Contains("simctl") && args.Contains("install"), exitCode: 0);
        
        // Screenshot capture
        SetupCommand("xcrun", args => args.Contains("simctl") && args.Contains("io") && args.Contains("screenshot"), exitCode: 0);
    }

    /// <inheritdoc />
    public Task<CommandResult> RunAsync(
        string command,
        string[]? arguments = null,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        arguments ??= [];
        var executedCommand = new ExecutedCommand(command, arguments, workingDirectory, timeout);
        _executedCommands.Add(executedCommand);

        // Try exact match first
        var key = CreateCommandKey(command, arguments);
        if (_commandSetups.TryGetValue(key, out var exactSetup))
        {
            return Task.FromResult(CreateResult(exactSetup));
        }

        // Try matcher-based setups
        var matcherSetup = _matcherSetups.FirstOrDefault(m => 
            m.Command == command && m.ArgumentMatcher(arguments));
        
        if (matcherSetup != null)
        {
            return Task.FromResult(CreateResult(matcherSetup.Setup));
        }

        // Default successful response
        return Task.FromResult(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "",
            StandardError = "",
            ExecutionTime = TimeSpan.FromMilliseconds(100)
        });
    }

    /// <inheritdoc />
    public Task<CommandResult> RunWithRetryAsync(
        string command,
        string[]? arguments = null,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        int retryCount = 3,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default)
    {
        // For testing, just delegate to RunAsync
        return RunAsync(command, arguments, workingDirectory, timeout, cancellationToken);
    }

    /// <summary>
    /// Clears all executed commands and setups.
    /// </summary>
    public void Reset()
    {
        _executedCommands.Clear();
        _commandSetups.Clear();
        _matcherSetups.Clear();
    }

    /// <summary>
    /// Verifies that a command was executed with the specified arguments.
    /// </summary>
    /// <param name="command">Expected command</param>
    /// <param name="arguments">Expected arguments</param>
    /// <returns>True if the command was executed</returns>
    public bool WasCommandExecuted(string command, params string[] arguments)
    {
        return _executedCommands.Any(ec => 
            ec.Command == command && ec.Arguments.SequenceEqual(arguments));
    }

    /// <summary>
    /// Verifies that a command was executed matching the specified predicate.
    /// </summary>
    /// <param name="predicate">Predicate to match against executed commands</param>
    /// <returns>True if a matching command was executed</returns>
    public bool WasCommandExecuted(Func<ExecutedCommand, bool> predicate)
    {
        return _executedCommands.Any(predicate);
    }

    /// <summary>
    /// Gets the count of how many times a specific command was executed.
    /// </summary>
    /// <param name="command">Command to count</param>
    /// <param name="arguments">Optional arguments to match</param>
    /// <returns>Number of times the command was executed</returns>
    public int GetExecutionCount(string command, string[]? arguments = null)
    {
        if (arguments == null)
        {
            return _executedCommands.Count(ec => ec.Command == command);
        }
        
        return _executedCommands.Count(ec => 
            ec.Command == command && ec.Arguments.SequenceEqual(arguments));
    }

    private static string CreateCommandKey(string command, string[] arguments)
    {
        return $"{command}|{string.Join("|", arguments)}";
    }

    private static CommandResult CreateResult(CommandSetup setup)
    {
        return new CommandResult
        {
            ExitCode = setup.ExitCode,
            StandardOutput = setup.StandardOutput,
            StandardError = setup.StandardError,
            ExecutionTime = setup.ExecutionTime
        };
    }

    private class CommandSetup
    {
        public int ExitCode { get; init; }
        public string StandardOutput { get; init; } = "";
        public string StandardError { get; init; } = "";
        public TimeSpan ExecutionTime { get; init; }
    }

    private class CommandMatcher
    {
        public string Command { get; init; } = "";
        public Func<string[], bool> ArgumentMatcher { get; init; } = _ => false;
        public CommandSetup Setup { get; init; } = new();
    }
}

/// <summary>
/// Represents a command that was executed during testing.
/// </summary>
/// <param name="Command">The command that was executed</param>
/// <param name="Arguments">The arguments passed to the command</param>
/// <param name="WorkingDirectory">The working directory used</param>
/// <param name="Timeout">The timeout specified</param>
public record ExecutedCommand(
    string Command,
    string[] Arguments,
    string? WorkingDirectory,
    TimeSpan? Timeout);