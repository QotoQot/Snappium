using Microsoft.Extensions.Logging;

namespace Snappium.Core.Logging;

/// <summary>
/// Console logger with Python-style colored output for Snappium.
/// </summary>
public sealed class SnappiumConsoleLogger : ISnappiumLogger
{
    readonly ILogger _logger;
    readonly bool _verboseMode;
    string _jobPrefix = string.Empty;

    const string RESET = "\x1b[0m";
    const string BLUE = "\x1b[34m";
    const string GREEN = "\x1b[32m";
    const string YELLOW = "\x1b[33m";
    const string RED = "\x1b[31m";
    const string GRAY = "\x1b[90m";

    public SnappiumConsoleLogger(ILogger logger, bool verboseMode = false)
    {
        _logger = logger;
        _verboseMode = verboseMode;
    }

    public void LogInfo(string message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        WriteColoredLine(BLUE, "INFO", formattedMessage);
        _logger.LogInformation(formattedMessage);
    }

    public void LogSuccess(string message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        WriteColoredLine(GREEN, "SUCCESS", formattedMessage);
        _logger.LogInformation(formattedMessage);
    }

    public void LogWarning(string message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        WriteColoredLine(YELLOW, "WARNING", formattedMessage);
        _logger.LogWarning(formattedMessage);
    }

    public void LogError(string message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        WriteColoredLine(RED, "ERROR", formattedMessage);
        _logger.LogError(formattedMessage);
    }

    public void LogError(Exception exception, string message, params object[] args)
    {
        var formattedMessage = FormatMessage(message, args);
        WriteColoredLine(RED, "ERROR", formattedMessage);
        _logger.LogError(exception, formattedMessage);
        
        if (_verboseMode)
        {
            WriteColoredLine(RED, "ERROR", $"Exception details: {exception}");
        }
    }

    public void LogDebug(string message, params object[] args)
    {
        if (!_verboseMode) return;
        
        var formattedMessage = FormatMessage(message, args);
        WriteColoredLine(GRAY, "DEBUG", formattedMessage);
        _logger.LogDebug(formattedMessage);
    }

    public void LogShellCommand(string command, string[] args, int exitCode, TimeSpan duration, string? workingDirectory = null)
    {
        var argsString = string.Join(" ", args.Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg));
        var fullCommand = $"{command} {argsString}";
        
        var statusColor = exitCode == 0 ? GREEN : RED;
        var statusText = exitCode == 0 ? "SUCCESS" : "FAILED";
        
        WriteColoredLine(statusColor, "SHELL", 
            $"[{statusText}] {fullCommand} (exit={exitCode}, {duration.TotalMilliseconds:F0}ms)");
        
        _logger.LogInformation("Shell command executed: {Command} {Args} (exit={ExitCode}, duration={Duration}ms, workdir={WorkingDirectory})",
            command, argsString, exitCode, duration.TotalMilliseconds, workingDirectory ?? ".");
        
        if (_verboseMode && !string.IsNullOrEmpty(workingDirectory))
        {
            WriteColoredLine(GRAY, "DEBUG", $"Working directory: {workingDirectory}");
        }
    }

    public IDisposable BeginJobScope(string jobId)
    {
        return new JobScope(this, jobId);
    }

    string FormatMessage(string message, params object[] args)
    {
        var formattedMessage = args.Length > 0 ? string.Format(message, args) : message;
        return string.IsNullOrEmpty(_jobPrefix) ? formattedMessage : $"[{_jobPrefix}] {formattedMessage}";
    }

    static void WriteColoredLine(string color, string level, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"{GRAY}[{timestamp}]{RESET} {color}[{level}]{RESET} {message}");
    }

    void SetJobPrefix(string prefix)
    {
        _jobPrefix = prefix;
    }

    void ClearJobPrefix()
    {
        _jobPrefix = string.Empty;
    }

    sealed class JobScope : IDisposable
    {
        readonly SnappiumConsoleLogger _logger;
        readonly string _originalPrefix;

        public JobScope(SnappiumConsoleLogger logger, string jobId)
        {
            _logger = logger;
            _originalPrefix = _logger._jobPrefix;
            _logger.SetJobPrefix(jobId);
        }

        public void Dispose()
        {
            _logger._jobPrefix = _originalPrefix;
        }
    }
}