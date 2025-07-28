using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Snappium.Core.Abstractions;
using Snappium.Core.Infrastructure;

namespace Snappium.Core.Build;

/// <summary>
/// Service for building .NET projects and discovering artifacts.
/// </summary>
public sealed class BuildService : IBuildService
{
    private readonly ICommandRunner _commandRunner;
    private readonly ILogger<BuildService> _logger;

    public BuildService(ICommandRunner commandRunner, ILogger<BuildService> logger)
    {
        _commandRunner = commandRunner;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BuildResult> BuildAsync(
        Platform platform,
        string csprojPath,
        string configuration = "Release",
        string? targetFramework = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(csprojPath))
        {
            return new BuildResult
            {
                Success = false,
                ErrorMessage = $"Project file not found: {csprojPath}"
            };
        }

        _logger.LogInformation("Building {Platform} project: {Project}", platform, csprojPath);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var args = new List<string> { "build", csprojPath, "-c", configuration };
            
            if (!string.IsNullOrEmpty(targetFramework))
            {
                args.AddRange(["-f", targetFramework]);
            }

            // Add platform-specific arguments
            if (platform == Platform.iOS)
            {
                // iOS builds may need specific runtime identifiers
                args.AddRange(["-p:RuntimeIdentifier=ios-arm64"]);
            }
            else if (platform == Platform.Android)
            {
                // Android builds may need specific properties
                args.AddRange(["-p:AndroidUseAapt2=true"]);
            }

            var result = await _commandRunner.RunAsync(
                "dotnet",
                [.. args],
                timeout: TimeSpan.FromMinutes(10),
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Build completed successfully in {Duration}ms", stopwatch.ElapsedMilliseconds);
                
                // Try to discover the output directory
                var outputDir = await GetOutputDirectoryAsync(csprojPath, configuration, targetFramework);
                
                return new BuildResult
                {
                    Success = true,
                    OutputDirectory = outputDir,
                    Duration = stopwatch.Elapsed
                };
            }
            else
            {
                _logger.LogError("Build failed with exit code {ExitCode}: {Error}", result.ExitCode, result.StandardError);
                return new BuildResult
                {
                    Success = false,
                    ErrorMessage = result.StandardError,
                    Duration = stopwatch.Elapsed
                };
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Build failed with exception");
            return new BuildResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <inheritdoc />
    public Task<string?> DiscoverArtifactAsync(string searchPattern, string? baseDirectory = null)
    {
        baseDirectory ??= Environment.CurrentDirectory;
        
        _logger.LogDebug("Discovering artifacts with pattern '{Pattern}' in '{BaseDirectory}'", 
            searchPattern, baseDirectory);

        try
        {
            // Use Directory.GetFiles with SearchOption.AllDirectories for recursive search
            var files = Directory.GetFiles(baseDirectory, searchPattern, SearchOption.AllDirectories);
            
            if (files.Length == 0)
            {
                _logger.LogDebug("No artifacts found matching pattern '{Pattern}'", searchPattern);
                return null;
            }

            // Get the most recently modified file
            var latestFile = files
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTime)
                .First();

            _logger.LogInformation("Discovered artifact: {Path} (modified: {Modified})", 
                latestFile.FullName, latestFile.LastWriteTime);

            return Task.FromResult<string?>(latestFile.FullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering artifacts with pattern '{Pattern}'", searchPattern);
            return Task.FromResult<string?>(null);
        }
    }

    private Task<string?> GetOutputDirectoryAsync(string csprojPath, string configuration, string? targetFramework)
    {
        try
        {
            var projectDir = Path.GetDirectoryName(csprojPath);
            if (projectDir == null) return Task.FromResult<string?>(null);

            // Standard .NET output structure: bin/{configuration}/{tfm}/
            var binDir = Path.Combine(projectDir, "bin", configuration);
            
            if (!Directory.Exists(binDir))
            {
                return Task.FromResult<string?>(null);
            }

            // If target framework is specified, look for that specific directory
            if (!string.IsNullOrEmpty(targetFramework))
            {
                var tfmDir = Path.Combine(binDir, targetFramework);
                if (Directory.Exists(tfmDir))
                {
                    return Task.FromResult<string?>(tfmDir);
                }
            }

            // Otherwise, find the most recent subdirectory (target framework)
            var subdirs = Directory.GetDirectories(binDir);
            if (subdirs.Length > 0)
            {
                var latestDir = subdirs
                    .Select(d => new DirectoryInfo(d))
                    .OrderByDescending(di => di.LastWriteTime)
                    .First();
                
                return Task.FromResult<string?>(latestDir.FullName);
            }

            return Task.FromResult<string?>(binDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining output directory for {Project}", csprojPath);
            return Task.FromResult<string?>(null);
        }
    }
}