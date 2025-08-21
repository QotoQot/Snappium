using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snappium.Core.Config;
using Snappium.Core.Infrastructure;

namespace Snappium.Core.Build;

/// <summary>
/// Controller for managing local Appium server instances with proper process tracking.
/// </summary>
public sealed class AppiumServerController : IAppiumServerController
{
    private readonly ICommandRunner _commandRunner;
    private readonly ILogger<AppiumServerController> _logger;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<int, AppiumServerInstance> _runningServers;

    /// <summary>
    /// Represents a managed Appium server instance.
    /// </summary>
    private sealed record AppiumServerInstance
    {
        public required Process Process { get; init; }
        public required int Port { get; init; }
        public required string ServerUrl { get; init; }
        public required DateTime StartTime { get; init; }
    }

    public AppiumServerController(ICommandRunner commandRunner, ILogger<AppiumServerController> logger)
    {
        _commandRunner = commandRunner;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _runningServers = new ConcurrentDictionary<int, AppiumServerInstance>();
    }

    /// <inheritdoc />
    public async Task<AppiumServerResult> StartServerAsync(int port, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Appium server on port {Port}", port);

        try
        {
            // Check if we already have a managed server on this port
            if (_runningServers.TryGetValue(port, out var existingServer))
            {
                if (!existingServer.Process.HasExited)
                {
                    _logger.LogInformation("Appium server already running on port {Port} (PID: {ProcessId})", 
                        port, existingServer.Process.Id);
                    return new AppiumServerResult
                    {
                        Success = true,
                        ServerUrl = existingServer.ServerUrl,
                        ProcessId = existingServer.Process.Id
                    };
                }
                else
                {
                    // Clean up dead process entry
                    _runningServers.TryRemove(port, out _);
                    existingServer.Process.Dispose();
                }
            }

            // Check if port is already in use by external process
            if (await IsServerRunningAsync(port, cancellationToken))
            {
                _logger.LogInformation("Port {Port} is already in use by an existing Appium server, reusing it", port);
                var externalServerUrl = $"http://localhost:{port}";
                
                return new AppiumServerResult
                {
                    Success = true,
                    ServerUrl = externalServerUrl,
                    ProcessId = null // External process, we don't manage it
                };
            }

            // Start Appium server
            var args = new List<string> { "--port", port.ToString(), "--log-level", "warn" };
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "appium",
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                return new AppiumServerResult
                {
                    Success = false,
                    ErrorMessage = "Failed to start Appium process"
                };
            }

            _logger.LogDebug("Started Appium process {ProcessId} on port {Port}", process.Id, port);

            // Wait for server to be ready
            var serverUrl = $"http://localhost:{port}";
            var ready = await WaitForServerReadyAsync(serverUrl, TimeSpan.FromSeconds(30), cancellationToken);

            if (ready)
            {
                // Store the server instance for proper tracking
                var serverInstance = new AppiumServerInstance
                {
                    Process = process,
                    Port = port,
                    ServerUrl = serverUrl,
                    StartTime = DateTime.UtcNow
                };

                _runningServers.TryAdd(port, serverInstance);

                _logger.LogInformation("Appium server started successfully on {Url} (PID: {ProcessId})", 
                    serverUrl, process.Id);
                
                return new AppiumServerResult
                {
                    Success = true,
                    ServerUrl = serverUrl,
                    ProcessId = process.Id
                };
            }
            else
            {
                _logger.LogError("Appium server failed to become ready within timeout on port {Port}", port);
                
                // Clean up failed process
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error killing failed Appium process {ProcessId}", process.Id);
                }
                finally
                {
                    process.Dispose();
                }

                return new AppiumServerResult
                {
                    Success = false,
                    ErrorMessage = "Appium server failed to become ready within timeout"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Appium server on port {Port}", port);
            return new AppiumServerResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<AppiumServerResult> StopServerAsync(int port, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping Appium server on port {Port}", port);

        try
        {
            // First try to stop our managed server instance
            if (_runningServers.TryRemove(port, out var serverInstance))
            {
                var success = await StopManagedServerAsync(serverInstance, cancellationToken);
                if (success)
                {
                    return new AppiumServerResult { Success = true };
                }
                
                // If graceful stop failed, fall back to force kill
                _logger.LogWarning("Graceful stop failed for port {Port}, attempting force kill", port);
            }

            // Fall back to port-based cleanup for external processes
            var killed = await KillProcessOnPortAsync(port, cancellationToken);
            
            return new AppiumServerResult
            {
                Success = killed,
                ErrorMessage = killed ? null : "Failed to kill process on port"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Appium server on port {Port}", port);
            return new AppiumServerResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsServerRunningAsync(int port, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"http://localhost:{port}/status";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var status = JsonSerializer.Deserialize<JsonElement>(content);
                
                // Check if it's an Appium server by looking for the status response
                return status.TryGetProperty("value", out _);
            }
        }
        catch
        {
            // Server not reachable
        }

        return false;
    }

    /// <summary>
    /// Gracefully stops a managed Appium server instance.
    /// </summary>
    private async Task<bool> StopManagedServerAsync(AppiumServerInstance serverInstance, CancellationToken cancellationToken)
    {
        try
        {
            var process = serverInstance.Process;
            var port = serverInstance.Port;
            
            _logger.LogDebug("Stopping managed Appium server on port {Port} (PID: {ProcessId})", port, process.Id);
            
            if (process.HasExited)
            {
                _logger.LogDebug("Process {ProcessId} already exited", process.Id);
                process.Dispose();
                return true;
            }

            // Try graceful shutdown first by closing main window (if available)
            if (!process.CloseMainWindow())
            {
                _logger.LogDebug("CloseMainWindow failed for process {ProcessId}, using Kill", process.Id);
            }

            // Wait up to 5 seconds for graceful shutdown
            var gracefulShutdown = await Task.Run(() => process.WaitForExit(5000), cancellationToken);
            
            if (gracefulShutdown)
            {
                _logger.LogDebug("Appium server {ProcessId} shut down gracefully", process.Id);
                process.Dispose();
                return true;
            }

            // Force kill if graceful shutdown didn't work
            _logger.LogDebug("Force killing Appium server {ProcessId}", process.Id);
            process.Kill();
            
            // Wait up to 3 seconds for force kill to complete
            var forceKilled = await Task.Run(() => process.WaitForExit(3000), cancellationToken);
            process.Dispose();
            
            if (forceKilled)
            {
                _logger.LogDebug("Appium server {ProcessId} force killed successfully", process.Id);
                return true;
            }
            else
            {
                _logger.LogError("Failed to kill Appium server {ProcessId} within timeout", process.Id);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping managed Appium server on port {Port}", serverInstance.Port);
            try
            {
                serverInstance.Process.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> KillProcessOnPortAsync(int port, CancellationToken cancellationToken = default)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await KillProcessOnPortWindowsAsync(port, cancellationToken);
            }
            else
            {
                return await KillProcessOnPortUnixAsync(port, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error killing process on port {Port}", port);
            return false;
        }
    }

    private async Task<bool> WaitForServerReadyAsync(string serverUrl, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var endTime = DateTime.UtcNow.Add(timeout);
        
        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{serverUrl}/status", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Server not ready yet
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        return false;
    }

    private async Task<bool> KillProcessOnPortWindowsAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            // Find process using the port
            var netstatResult = await _commandRunner.RunAsync(
                "netstat",
                ["-ano"],
                timeout: Defaults.Timeouts.ShortOperation,
                cancellationToken: cancellationToken);

            if (netstatResult.ExitCode != 0)
            {
                return false;
            }

            var lines = netstatResult.StandardOutput.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains($":{port} ") && line.Contains("LISTENING"))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && int.TryParse(parts[^1], out var pid))
                    {
                        var killResult = await _commandRunner.RunAsync(
                            "taskkill",
                            ["/F", "/PID", pid.ToString()],
                            timeout: Defaults.Timeouts.ShortOperation,
                            cancellationToken: cancellationToken);

                        return killResult.ExitCode == 0;
                    }
                }
            }

            return true; // No process found on port
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error killing process on port {Port} (Windows)", port);
            return false;
        }
    }

    private async Task<bool> KillProcessOnPortUnixAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            // Find process using the port
            var lsofResult = await _commandRunner.RunAsync(
                "lsof",
                ["-t", $"-i:{port}"],
                timeout: Defaults.Timeouts.ShortOperation,
                cancellationToken: cancellationToken);

            if (lsofResult.ExitCode != 0 || string.IsNullOrWhiteSpace(lsofResult.StandardOutput))
            {
                return true; // No process found on port
            }

            var pids = lsofResult.StandardOutput.Trim().Split('\n');
            foreach (var pidStr in pids)
            {
                if (int.TryParse(pidStr.Trim(), out var pid))
                {
                    var killResult = await _commandRunner.RunAsync(
                        "kill",
                        ["-9", pid.ToString()],
                        timeout: Defaults.Timeouts.ShortOperation,
                        cancellationToken: cancellationToken);

                    if (killResult.ExitCode != 0)
                    {
                        _logger.LogWarning("Failed to kill process {Pid} on port {Port}", pid, port);
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error killing process on port {Port} (Unix)", port);
            return false;
        }
    }

    /// <summary>
    /// Stops all managed Appium servers.
    /// </summary>
    public async Task StopAllServersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping all {Count} managed Appium servers", _runningServers.Count);
        
        var stopTasks = new List<Task>();
        
        foreach (var kvp in _runningServers.ToArray())
        {
            var port = kvp.Key;
            var serverInstance = kvp.Value;
            
            if (_runningServers.TryRemove(port, out _))
            {
                stopTasks.Add(StopManagedServerAsync(serverInstance, cancellationToken));
            }
        }
        
        if (stopTasks.Count > 0)
        {
            await Task.WhenAll(stopTasks);
        }
        
        _logger.LogInformation("Finished stopping all managed Appium servers");
    }

    public void Dispose()
    {
        try
        {
            // Stop all managed servers synchronously during disposal
            var stopTask = StopAllServersAsync(CancellationToken.None);
            if (!stopTask.Wait(Defaults.Timeouts.ShortOperation))
            {
                _logger.LogWarning("Timeout waiting for servers to stop during disposal");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping servers during disposal");
        }
        finally
        {
            _httpClient?.Dispose();
        }
    }
}