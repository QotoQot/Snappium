using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Snappium.Core.Infrastructure;

/// <summary>
/// Global process manager for tracking and cleaning up all managed processes.
/// Ensures no zombie processes are left running on application termination.
/// </summary>
public sealed class ProcessManager : IDisposable
{
    readonly ILogger<ProcessManager> _logger;
    readonly ConcurrentDictionary<string, IManagedProcess> _managedProcesses;
    bool _disposed;

    public ProcessManager(ILogger<ProcessManager> logger)
    {
        _logger = logger;
        _managedProcesses = new ConcurrentDictionary<string, IManagedProcess>();
        
        // Register cleanup handlers for graceful shutdown
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Register a managed process for cleanup tracking.
    /// </summary>
    /// <param name="processId">Unique identifier for the process</param>
    /// <param name="managedProcess">The managed process instance</param>
    public void RegisterProcess(string processId, IManagedProcess managedProcess)
    {
        if (_disposed)
        {
            _logger.LogWarning("ProcessManager is disposed, cannot register process: {ProcessId}", processId);
            return;
        }

        _managedProcesses.TryAdd(processId, managedProcess);
        _logger.LogDebug("Registered managed process: {ProcessId} ({Type})", processId, managedProcess.GetType().Name);
    }

    /// <summary>
    /// Unregister a managed process (when it's been cleanly shut down).
    /// </summary>
    /// <param name="processId">Unique identifier for the process</param>
    public void UnregisterProcess(string processId)
    {
        if (_managedProcesses.TryRemove(processId, out var process))
        {
            _logger.LogDebug("Unregistered managed process: {ProcessId}", processId);
        }
    }

    /// <summary>
    /// Get count of currently managed processes.
    /// </summary>
    public int ManagedProcessCount => _managedProcesses.Count;

    /// <summary>
    /// Cleanup all managed processes gracefully.
    /// </summary>
    public async Task CleanupAllProcessesAsync(CancellationToken cancellationToken = default)
    {
        if (_managedProcesses.IsEmpty)
        {
            _logger.LogDebug("No managed processes to cleanup");
            return;
        }

        _logger.LogInformation("Cleaning up {Count} managed processes", _managedProcesses.Count);

        var cleanupTasks = new List<Task>();
        foreach (var kvp in _managedProcesses.ToArray())
        {
            var processId = kvp.Key;
            var process = kvp.Value;
            
            cleanupTasks.Add(CleanupProcessAsync(processId, process, cancellationToken));
        }

        try
        {
            // Wait for all cleanup tasks with a reasonable timeout
            await Task.WhenAll(cleanupTasks).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            _logger.LogInformation("All managed processes cleaned up successfully");
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timeout occurred during process cleanup - some processes may still be running");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during process cleanup");
        }
        finally
        {
            _managedProcesses.Clear();
        }
    }

    async Task CleanupProcessAsync(string processId, IManagedProcess managedProcess, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Cleaning up managed process: {ProcessId}", processId);
            await managedProcess.StopAsync(cancellationToken);
            _logger.LogDebug("Successfully cleaned up managed process: {ProcessId}", processId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup managed process: {ProcessId}", processId);
        }
        finally
        {
            _managedProcesses.TryRemove(processId, out _);
        }
    }

    void OnProcessExit(object? sender, EventArgs e)
    {
        _logger.LogInformation("Application process exit detected - cleaning up managed processes");
        CleanupAllProcessesAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        _logger.LogInformation("Ctrl+C detected - cleaning up managed processes");
        e.Cancel = true; // Prevent immediate termination
        
        // Cleanup processes synchronously since we're in a signal handler
        CleanupAllProcessesAsync(CancellationToken.None).GetAwaiter().GetResult();
        
        // Now allow normal termination
        Environment.Exit(0);
    }

    void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.LogCritical("Unhandled exception detected - cleaning up managed processes before termination");
        try
        {
            CleanupAllProcessesAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception cleanupEx)
        {
            _logger.LogError(cleanupEx, "Failed to cleanup processes during unhandled exception");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Unregister event handlers
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        Console.CancelKeyPress -= OnCancelKeyPress;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;

        // Final cleanup
        if (!_managedProcesses.IsEmpty)
        {
            _logger.LogWarning("ProcessManager disposing with {Count} managed processes still registered", _managedProcesses.Count);
            CleanupAllProcessesAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        _disposed = true;
    }
}

/// <summary>
/// Interface for processes that can be managed by the ProcessManager.
/// </summary>
public interface IManagedProcess
{
    /// <summary>
    /// Stop the managed process gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when the process is stopped</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}