using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Snappium.Core.Infrastructure;

namespace Snappium.Core.Build;

/// <summary>
/// Service for checking required dependencies and tools.
/// </summary>
public sealed class DependencyChecker : IDependencyChecker
{
    private readonly ICommandRunner _commandRunner;
    private readonly ILogger<DependencyChecker> _logger;

    public DependencyChecker(ICommandRunner commandRunner, ILogger<DependencyChecker> logger)
    {
        _commandRunner = commandRunner;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DependencyCheckResult> CheckDependenciesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking dependencies...");
        
        var dependencies = new List<DependencyResult>();
        
        // Check .NET (always required)
        dependencies.Add(await CheckDotNetAsync(cancellationToken));
        
        // Check platform-specific tools
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            dependencies.Add(await CheckXcrunAsync(cancellationToken));
        }
        
        dependencies.Add(await CheckAdbAsync(cancellationToken));
        
        // Check optional tools
        dependencies.Add(await CheckAppiumAsync(cancellationToken));
        
        var requiredDependencies = dependencies.Where(d => d.IsRequired).ToList();
        var allRequiredAvailable = requiredDependencies.All(d => d.IsAvailable);
        
        var warnings = new List<string>();
        foreach (var dep in dependencies.Where(d => !d.IsRequired && !d.IsAvailable))
        {
            warnings.Add($"Optional dependency '{dep.Name}' not found: {dep.ErrorMessage}");
        }
        
        var result = new DependencyCheckResult
        {
            AllRequiredAvailable = allRequiredAvailable,
            Dependencies = dependencies,
            Warnings = warnings
        };
        
        if (allRequiredAvailable)
        {
            _logger.LogInformation("All required dependencies are available");
        }
        else
        {
            var missing = requiredDependencies.Where(d => !d.IsAvailable).Select(d => d.Name);
            _logger.LogError("Missing required dependencies: {Dependencies}", string.Join(", ", missing));
        }
        
        return result;
    }

    /// <inheritdoc />
    public async Task<DependencyResult> CheckDotNetAsync(CancellationToken cancellationToken = default)
    {
        return await CheckCommandAsync("dotnet", ["--version"], "dotnet", isRequired: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DependencyResult> CheckXcrunAsync(CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new DependencyResult
            {
                Name = "xcrun",
                IsAvailable = false,
                IsRequired = false,
                ErrorMessage = "Not running on macOS"
            };
        }
        
        return await CheckCommandAsync("xcrun", ["--version"], "xcrun", isRequired: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DependencyResult> CheckAdbAsync(CancellationToken cancellationToken = default)
    {
        return await CheckCommandAsync("adb", ["version"], "adb", isRequired: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DependencyResult> CheckAppiumAsync(CancellationToken cancellationToken = default)
    {
        return await CheckCommandAsync("appium", ["--version"], "appium", isRequired: false, cancellationToken);
    }

    private async Task<DependencyResult> CheckCommandAsync(
        string command,
        string[] args,
        string displayName,
        bool isRequired,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync(
                command,
                args,
                timeout: TimeSpan.FromSeconds(10),
                cancellationToken: cancellationToken);

            if (result.ExitCode == 0)
            {
                var version = result.StandardOutput.Trim();
                _logger.LogDebug("{Command} found: {Version}", displayName, version);
                
                return new DependencyResult
                {
                    Name = displayName,
                    IsAvailable = true,
                    IsRequired = isRequired,
                    Version = version,
                    Path = command // Could be enhanced to find actual path
                };
            }
            else
            {
                var error = $"Command failed with exit code {result.ExitCode}: {result.StandardError}";
                _logger.LogWarning("{Command} check failed: {Error}", displayName, error);
                
                return new DependencyResult
                {
                    Name = displayName,
                    IsAvailable = false,
                    IsRequired = isRequired,
                    ErrorMessage = error
                };
            }
        }
        catch (Exception ex)
        {
            var error = $"Exception: {ex.Message}";
            if (isRequired)
            {
                _logger.LogError(ex, "{Command} check failed", displayName);
            }
            else
            {
                _logger.LogWarning("{Command} check failed: {Error}", displayName, error);
            }
            
            return new DependencyResult
            {
                Name = displayName,
                IsAvailable = false,
                IsRequired = isRequired,
                ErrorMessage = error
            };
        }
    }
}