namespace Snappium.Core.Logging;

/// <summary>
/// Enhanced logger interface for Snappium with colored output support.
/// </summary>
public interface ISnappiumLogger
{
    /// <summary>
    /// Log an informational message (blue).
    /// </summary>
    void LogInfo(string message, params object[] args);
    
    /// <summary>
    /// Log a success message (green).
    /// </summary>
    void LogSuccess(string message, params object[] args);
    
    /// <summary>
    /// Log a warning message (yellow).
    /// </summary>
    void LogWarning(string message, params object[] args);
    
    /// <summary>
    /// Log an error message (red).
    /// </summary>
    void LogError(string message, params object[] args);
    
    /// <summary>
    /// Log an error with exception (red).
    /// </summary>
    void LogError(Exception exception, string message, params object[] args);
    
    /// <summary>
    /// Log a debug message (only shown in verbose mode).
    /// </summary>
    void LogDebug(string message, params object[] args);
    
    /// <summary>
    /// Log a shell command execution with structured data.
    /// </summary>
    void LogShellCommand(string command, string[] args, int exitCode, TimeSpan duration, string? workingDirectory = null);
    
    /// <summary>
    /// Create a scoped logger with a job prefix.
    /// </summary>
    IDisposable BeginJobScope(string jobId);
}