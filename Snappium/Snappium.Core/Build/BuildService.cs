using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Snappium.Core.Abstractions;
using Snappium.Core.Config;
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
                timeout: Defaults.Timeouts.BuildOperation,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Build completed successfully in {Duration}ms", stopwatch.ElapsedMilliseconds);
                
                // Get actual output directory from MSBuild instead of guessing
                var outputDir = await GetOutputDirectoryFromMSBuildAsync(csprojPath, configuration, targetFramework, platform, cancellationToken);
                
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
            return Task.FromResult<string?>(null!);
        }
    }

    /// <summary>
    /// Gets the actual output directory by querying MSBuild properties instead of guessing paths.
    /// This is more reliable than path guessing since it respects custom OutputPath, TFM, and RID settings.
    /// </summary>
    private async Task<string?> GetOutputDirectoryFromMSBuildAsync(
        string csprojPath,
        string configuration,
        string? targetFramework,
        Platform platform,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Querying MSBuild for output directory: {Project}", csprojPath);

            var args = new List<string>
            {
                "msbuild",
                csprojPath,
                "-getProperty:OutputPath",
                "-nologo",
                $"-p:Configuration={configuration}"
            };

            if (!string.IsNullOrEmpty(targetFramework))
            {
                args.Add($"-p:TargetFramework={targetFramework}");
            }

            // Add platform-specific properties to match build configuration
            if (platform == Platform.iOS)
            {
                args.Add("-p:RuntimeIdentifier=ios-arm64");
            }
            else if (platform == Platform.Android)
            {
                args.Add("-p:AndroidUseAapt2=true");
            }

            var result = await _commandRunner.RunAsync(
                "dotnet",
                [.. args],
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            if (result.ExitCode == 0)
            {
                // The -getProperty:OutputPath option returns the property value directly
                var outputPath = result.StandardOutput.Trim();
                
                if (!string.IsNullOrEmpty(outputPath))
                {
                    // Convert relative path to absolute path
                    if (!Path.IsPathRooted(outputPath))
                    {
                        var projectDir = Path.GetDirectoryName(csprojPath);
                        if (projectDir != null)
                        {
                            outputPath = Path.GetFullPath(Path.Combine(projectDir, outputPath));
                        }
                    }

                    _logger.LogDebug("MSBuild reported output directory: {OutputPath}", outputPath);
                    return outputPath;
                }
            }

            _logger.LogWarning("Failed to get output directory from MSBuild, exit code: {ExitCode}, output: {Output}",
                result.ExitCode, result.StandardOutput);

            // Fallback to conventional path guessing as last resort
            return GetConventionalOutputPath(csprojPath, configuration, targetFramework);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying MSBuild for output directory: {Project}", csprojPath);
            
            // Fallback to conventional path guessing
            return GetConventionalOutputPath(csprojPath, configuration, targetFramework);
        }
    }

    /// <summary>
    /// Fallback method that uses conventional .NET output path structure.
    /// Used when MSBuild query fails.
    /// </summary>
    private string? GetConventionalOutputPath(string csprojPath, string configuration, string? targetFramework)
    {
        try
        {
            var projectDir = Path.GetDirectoryName(csprojPath);
            if (projectDir == null) return null;

            // Standard .NET output structure: bin/{configuration}/{tfm}/
            var binDir = Path.Combine(projectDir, "bin", configuration);
            
            if (!Directory.Exists(binDir))
            {
                return null;
            }

            // If target framework is specified, look for that specific directory
            if (!string.IsNullOrEmpty(targetFramework))
            {
                var tfmDir = Path.Combine(binDir, targetFramework);
                if (Directory.Exists(tfmDir))
                {
                    return tfmDir;
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
                
                return latestDir.FullName;
            }

            return binDir;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining conventional output path for {Project}", csprojPath);
            return null;
        }
    }
}