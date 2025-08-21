using Microsoft.Extensions.Logging;
using Snappium.Core.Abstractions;
using Snappium.Core.Config;
using System.Text.RegularExpressions;

namespace Snappium.Core.Planning;

/// <summary>
/// Builds a list of RunJob instances from configuration and filters.
/// </summary>
public sealed class RunPlanBuilder
{
    private readonly ILogger<RunPlanBuilder> _logger;

    public RunPlanBuilder(ILogger<RunPlanBuilder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Build a complete run plan from configuration and optional filters.
    /// </summary>
    /// <param name="config">Root configuration</param>
    /// <param name="outputRoot">Root directory for outputs</param>
    /// <param name="platformFilter">Optional platform filter (e.g., "ios", "android")</param>
    /// <param name="deviceFilter">Optional device name filter</param>
    /// <param name="languageFilter">Optional language filter</param>
    /// <param name="screenshotFilter">Optional screenshot name filter</param>
    /// <param name="portAllocator">Port allocator for assigning unique ports</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of jobs to execute</returns>
    public async Task<RunPlan> BuildAsync(
        RootConfig config,
        string outputRoot,
        string[]? platformFilter = null,
        string[]? deviceFilter = null,
        string[]? languageFilter = null,
        string[]? screenshotFilter = null,
        PortAllocator? portAllocator = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building run plan with filters - Platforms: {Platforms}, Devices: {Devices}, Languages: {Languages}, Screenshots: {Screenshots}",
            platformFilter?.Length > 0 ? string.Join(",", platformFilter) : "all",
            deviceFilter?.Length > 0 ? string.Join(",", deviceFilter) : "all",
            languageFilter?.Length > 0 ? string.Join(",", languageFilter) : "all",
            screenshotFilter?.Length > 0 ? string.Join(",", screenshotFilter) : "all");

        portAllocator ??= new PortAllocator(config.Ports?.BasePort ?? Defaults.Ports.AppiumBasePort, config.Ports?.PortOffset ?? Defaults.Ports.PortOffset);

        var jobs = new List<RunJob>();
        var artifactPaths = await ResolveArtifactPathsAsync(config, cancellationToken);

        // Filter platforms
        var platforms = GetFilteredPlatforms(platformFilter);

        // Filter languages
        var languages = GetFilteredLanguages(config, languageFilter);

        // Filter screenshots
        var screenshots = GetFilteredScreenshots(config, screenshotFilter);

        int jobIndex = 0;

        foreach (var platform in platforms)
        {
            var devices = GetFilteredDevices(config, platform, deviceFilter);

            foreach (var language in languages)
            {
                foreach (var device in devices)
                {
                    var job = await CreateJobAsync(
                        config,
                        platform,
                        device,
                        language,
                        screenshots,
                        outputRoot,
                        artifactPaths,
                        portAllocator.AllocatePortsForJob(jobIndex),
                        cancellationToken);

                    if (job != null)
                    {
                        jobs.Add(job);
                        jobIndex++;
                    }
                }
            }
        }

        var plan = new RunPlan
        {
            Jobs = jobs,
            TotalPlatforms = platforms.Length,
            TotalDevices = platforms.Sum(p => GetFilteredDevices(config, p, deviceFilter).Length),
            TotalLanguages = languages.Length,
            TotalScreenshots = screenshots.Count,
            EstimatedDurationMinutes = EstimateDuration(jobs),
            ArtifactPaths = artifactPaths
        };

        _logger.LogInformation("Built run plan: {JobCount} jobs across {Platforms} platforms, {Languages} languages",
            jobs.Count, plan.TotalPlatforms, plan.TotalLanguages);

        return plan;
    }

    private static Platform[] GetFilteredPlatforms(string[]? platformFilter)
    {
        if (platformFilter == null || platformFilter.Length == 0)
        {
            return [Platform.iOS, Platform.Android];
        }

        return platformFilter
            .Where(p => Enum.TryParse<Platform>(p, true, out _))
            .Select(p => Enum.Parse<Platform>(p, true))
            .Distinct()
            .ToArray();
    }

    private static string[] GetFilteredLanguages(RootConfig config, string[]? languageFilter)
    {
        var availableLanguages = config.Languages;

        if (languageFilter == null || languageFilter.Length == 0)
        {
            return availableLanguages.ToArray();
        }

        return languageFilter
            .Where(lang => availableLanguages.Contains(lang))
            .ToArray();
    }

    private static List<ScreenshotPlan> GetFilteredScreenshots(RootConfig config, string[]? screenshotFilter)
    {
        if (screenshotFilter == null || screenshotFilter.Length == 0)
        {
            return config.Screenshots;
        }

        return config.Screenshots
            .Where(s => screenshotFilter.Contains(s.Name))
            .ToList();
    }

    private static object[] GetFilteredDevices(RootConfig config, Platform platform, string[]? deviceFilter)
    {
        return platform switch
        {
            Platform.iOS => FilterDevices(config.Devices.Ios.Cast<object>(), deviceFilter, d => ((IosDevice)d).Name),
            Platform.Android => FilterDevices(config.Devices.Android.Cast<object>(), deviceFilter, d => ((AndroidDevice)d).Name),
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
    }

    private static object[] FilterDevices(IEnumerable<object> devices, string[]? deviceFilter, Func<object, string> nameSelector)
    {
        if (deviceFilter == null || deviceFilter.Length == 0)
        {
            return devices.ToArray();
        }

        return devices
            .Where(d => deviceFilter.Contains(nameSelector(d)))
            .ToArray();
    }

    private async Task<Dictionary<Platform, string>> ResolveArtifactPathsAsync(RootConfig config, CancellationToken cancellationToken)
    {
        var paths = new Dictionary<Platform, string>();

        if (config.BuildConfig?.Ios?.ArtifactGlob != null)
        {
            var iosPath = await ResolveGlobPatternAsync(config.BuildConfig.Ios.ArtifactGlob, cancellationToken);
            if (!string.IsNullOrEmpty(iosPath))
            {
                paths[Platform.iOS] = iosPath;
            }
        }

        if (config.BuildConfig?.Android?.ArtifactGlob != null)
        {
            var androidPath = await ResolveGlobPatternAsync(config.BuildConfig.Android.ArtifactGlob, cancellationToken);
            if (!string.IsNullOrEmpty(androidPath))
            {
                paths[Platform.Android] = androidPath;
            }
        }

        return paths;
    }

    private static async Task<string?> ResolveGlobPatternAsync(string globPattern, CancellationToken cancellationToken)
    {
        // Simple glob resolution - in a real implementation, you'd use a proper glob library
        var directory = Path.GetDirectoryName(globPattern) ?? ".";
        var pattern = Path.GetFileName(globPattern);

        // Ensure we have an absolute path for the directory
        if (!Path.IsPathRooted(directory))
        {
            directory = Path.GetFullPath(directory);
        }

        if (!Directory.Exists(directory))
        {
            return null;
        }

        // Convert simple glob to regex (basic implementation)
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);

        await Task.Yield(); // Make this async for consistency

        // Search both files and directories to handle iOS .app bundles (which are directories)
        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Where(f => regex.IsMatch(Path.GetFileName(f)))
            .ToArray();

        var directories = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories)
            .Where(d => regex.IsMatch(Path.GetDirectoryName(d) == directory ? Path.GetFileName(d) : Path.GetFileName(d)))
            .ToArray();

        // Combine files and directories, prioritizing directories for iOS apps
        var allMatches = directories.Concat(files).ToArray();

        // Return the most recently modified match with absolute path
        return allMatches.Length > 0
            ? Path.GetFullPath(allMatches.OrderByDescending(path => 
                Directory.Exists(path) ? new DirectoryInfo(path).LastWriteTime : new FileInfo(path).LastWriteTime).First())
            : null;
    }

    private async Task<RunJob?> CreateJobAsync(
        RootConfig config,
        Platform platform,
        object device,
        string language,
        List<ScreenshotPlan> screenshots,
        string outputRoot,
        Dictionary<Platform, string> artifactPaths,
        PortAllocation ports,
        CancellationToken cancellationToken)
    {
        if (!config.LocaleMapping.TryGetValue(language, out var localeMapping))
        {
            _logger.LogWarning("No locale mapping found for language {Language}, skipping", language);
            return null;
        }

        if (!artifactPaths.TryGetValue(platform, out var appPath))
        {
            // Allow missing artifacts if we can build (BuildConfig is available)
            var canBuild = config.BuildConfig != null && 
                          (platform == Platform.iOS ? config.BuildConfig.Ios?.Csproj != null : 
                           config.BuildConfig.Android?.Csproj != null);
            
            if (canBuild)
            {
                _logger.LogDebug("No app artifact found for platform {Platform}, but build is possible", platform);
                appPath = null; // Will be built during job execution
            }
            else
            {
                _logger.LogWarning("No app artifact found for platform {Platform} and no build config available, skipping", platform);
                return null;
            }
        }

        await Task.Yield(); // Make this async for consistency

        var outputDirectory = platform switch
        {
            Platform.iOS when device is IosDevice iosDevice => Path.Combine(outputRoot, "iOS", iosDevice.Folder, language),
            Platform.Android when device is AndroidDevice androidDevice => Path.Combine(outputRoot, "Android", androidDevice.Folder, language),
            _ => throw new ArgumentException($"Invalid device type for platform {platform}")
        };

        return new RunJob
        {
            Platform = platform,
            IosDevice = device as IosDevice,
            AndroidDevice = device as AndroidDevice,
            Language = language,
            LocaleMapping = localeMapping,
            OutputDirectory = outputDirectory,
            Screenshots = screenshots,
            Ports = ports,
            AppPath = appPath
        };
    }

    private static double EstimateDuration(List<RunJob> jobs)
    {
        // Rough estimation: 2 minutes per job
        return jobs.Count * 2.0;
    }
}

/// <summary>
/// A complete plan for a screenshot automation run.
/// </summary>
public sealed class RunPlan
{
    /// <summary>
    /// All jobs to execute in this run.
    /// </summary>
    public required List<RunJob> Jobs { get; init; }

    /// <summary>
    /// Number of platforms being tested.
    /// </summary>
    public required int TotalPlatforms { get; init; }

    /// <summary>
    /// Number of devices being tested.
    /// </summary>
    public required int TotalDevices { get; init; }

    /// <summary>
    /// Number of languages being tested.
    /// </summary>
    public required int TotalLanguages { get; init; }

    /// <summary>
    /// Number of screenshots being captured per job.
    /// </summary>
    public required int TotalScreenshots { get; init; }

    /// <summary>
    /// Estimated duration of the run in minutes.
    /// </summary>
    public required double EstimatedDurationMinutes { get; init; }

    /// <summary>
    /// Resolved artifact paths by platform.
    /// </summary>
    public required Dictionary<Platform, string> ArtifactPaths { get; init; }
}