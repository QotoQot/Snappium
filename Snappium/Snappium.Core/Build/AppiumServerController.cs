using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snappium.Core.Infrastructure;

namespace Snappium.Core.Build;

/// <summary>
/// Controller for managing local Appium server instances.
/// </summary>
public sealed class AppiumServerController : IAppiumServerController
{
    private readonly ICommandRunner _commandRunner;
    private readonly ILogger<AppiumServerController> _logger;
    private readonly HttpClient _httpClient;

    public AppiumServerController(ICommandRunner commandRunner, ILogger<AppiumServerController> logger)
    {
        _commandRunner = commandRunner;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <inheritdoc />
    public async Task<AppiumServerResult> StartServerAsync(int port, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Appium server on port {Port}", port);

        try
        {
            // Check if port is already in use
            if (await IsServerRunningAsync(port, cancellationToken))
            {
                _logger.LogInformation("Appium server already running on port {Port}", port);
                return new AppiumServerResult
                {
                    Success = true,
                    ServerUrl = $"http://localhost:{port}"
                };
            }

            // Kill any existing process on the port
            await KillProcessOnPortAsync(port, cancellationToken);

            // Start Appium server
            var args = new List<string> { "--port", port.ToString() };
            
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

            _logger.LogDebug("Started Appium process {ProcessId}", process.Id);

            // Wait for server to be ready
            var serverUrl = $"http://localhost:{port}";
            var ready = await WaitForServerReadyAsync(serverUrl, TimeSpan.FromSeconds(30), cancellationToken);

            if (ready)
            {
                _logger.LogInformation("Appium server started successfully on {Url}", serverUrl);
                return new AppiumServerResult
                {
                    Success = true,
                    ServerUrl = serverUrl,
                    ProcessId = process.Id
                };
            }
            else
            {
                process.Kill();
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
                timeout: TimeSpan.FromSeconds(10),
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
                            timeout: TimeSpan.FromSeconds(10),
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
                timeout: TimeSpan.FromSeconds(10),
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
                        timeout: TimeSpan.FromSeconds(10),
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

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}