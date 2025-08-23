using System.CommandLine;
using Microsoft.Extensions.Logging;
using Snappium.Core.Config;
using Snappium.Core.Orchestration;
using Snappium.Core.Planning;

namespace Snappium.Cli.Commands;

public class RunCommand : Command
{
    readonly IOrchestrator _orchestrator;
    readonly ConfigLoader _configLoader;
    readonly RunPlanBuilder _runPlanBuilder;
    readonly IManifestWriter _manifestWriter;
    readonly ILogger<RunCommand> _logger;
    readonly FilteringOptions _filteringOptions;

    public RunCommand(
        IOrchestrator orchestrator,
        ConfigLoader configLoader,
        RunPlanBuilder runPlanBuilder,
        IManifestWriter manifestWriter,
        ILogger<RunCommand> logger) : base("run", "Execute screenshot automation run")
    {
        _orchestrator = orchestrator;
        _configLoader = configLoader;
        _runPlanBuilder = runPlanBuilder;
        _manifestWriter = manifestWriter;
        _logger = logger;

        // Add shared filtering options
        _filteringOptions = SharedCommandOptions.AddFilteringOptions(this);

        // Add run-specific options
        var iosAppOption = new Option<FileInfo>(
            name: "--ios-app",
            description: "Path to iOS .app bundle");

        var androidAppOption = new Option<FileInfo>(
            name: "--android-app", 
            description: "Path to Android .apk file");

        var basePortOption = new Option<int?>(
            name: "--base-port",
            description: "Base port for Appium servers")
        {
            ArgumentHelpName = "port"
        };

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Print resolved plan without executing");

        var retryFailedOption = new Option<FileInfo>(
            name: "--retry-failed",
            description: "Retry failed jobs from manifest.json");

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Enable verbose logging with debug output")
        {
            IsHidden = true // Hidden since verbose is always enabled
        };

        // Add run-specific options (filtering options already added via SharedCommandOptions)
        AddOption(iosAppOption);
        AddOption(androidAppOption);
        AddOption(basePortOption);
        AddOption(dryRunOption);
        AddOption(retryFailedOption);
        AddOption(verboseOption);

        this.SetHandler(async (context) =>
        {
            var config = context.ParseResult.GetValueForOption(_filteringOptions.Config)!;
            var platforms = context.ParseResult.GetValueForOption(_filteringOptions.Platforms);
            var devices = context.ParseResult.GetValueForOption(_filteringOptions.Devices);
            var langs = context.ParseResult.GetValueForOption(_filteringOptions.Languages);
            var screens = context.ParseResult.GetValueForOption(_filteringOptions.Screenshots);
            var output = context.ParseResult.GetValueForOption(_filteringOptions.Output);
            var iosApp = context.ParseResult.GetValueForOption(iosAppOption);
            var androidApp = context.ParseResult.GetValueForOption(androidAppOption);
            var basePort = context.ParseResult.GetValueForOption(basePortOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var retryFailed = context.ParseResult.GetValueForOption(retryFailedOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            context.ExitCode = await ExecuteAsync(config, platforms, devices, langs, screens, 
                iosApp, androidApp, output, basePort, dryRun, retryFailed, verbose, context.GetCancellationToken());
        });
    }

    async Task<int> ExecuteAsync(
        FileInfo configFile,
        string[]? platforms,
        string[]? devices,
        string[]? langs,
        string[]? screens,
        FileInfo? iosApp,
        FileInfo? androidApp,
        DirectoryInfo? output,
        int? basePort,
        bool dryRun,
        FileInfo? retryFailed,
        bool verbose,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load configuration
            var config = await _configLoader.LoadAsync(configFile.FullName, schemaPath: null, cancellationToken);
            _logger.LogInformation("Loaded configuration from {ConfigPath}", configFile.FullName);

            var cliOverrides = new CliOverrides
            {
                IosAppPath = iosApp?.FullName,
                AndroidAppPath = androidApp?.FullName,
                OutputDirectory = output?.FullName,
                BasePort = basePort
            };

            // Create run plan with port allocation
            var configBasePort = config.Ports?.BasePort;
            var effectiveBasePort = basePort ?? configBasePort ?? Defaults.Ports.AppiumBasePort;
            var effectivePortOffset = config.Ports?.PortOffset ?? Defaults.Ports.PortOffset;
            var portAllocator = new PortAllocator(effectiveBasePort, effectivePortOffset);

            var outputRoot = output?.FullName ?? config.OutputDirectory ?? Path.Combine(Environment.CurrentDirectory, "Screenshots");
            
            var runPlan = await _runPlanBuilder.BuildAsync(
                config,
                outputRoot,
                platforms,
                devices,
                langs,
                screens,
                portAllocator,
                cancellationToken);

            _logger.LogInformation("Built run plan with {JobCount} jobs", runPlan.Jobs.Count);

            if (dryRun)
            {
                PrintDryRunPlan(runPlan, _logger);
                return 0;
            }

            // Execute the plan - the orchestrator handles its own scoping for parallel jobs
            var result = await _orchestrator.ExecuteAsync(runPlan, config, cliOverrides, cancellationToken);
            
            // Write manifest
            var manifestFiles = await _manifestWriter.WriteAsync(result, outputRoot, cancellationToken);

            _logger.LogInformation("Run completed. Success: {Success}", result.Success);
            _logger.LogInformation("Manifest: {ManifestPath}", manifestFiles.ManifestJsonPath);
            _logger.LogInformation("Summary: {SummaryPath}", manifestFiles.SummaryTextPath);

            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run failed with exception");
            return 1;
        }
    }

    static void PrintDryRunPlan(RunPlan runPlan, ILogger logger)
    {
        logger.LogInformation("=== DRY RUN - Resolved Plan ===");
        logger.LogInformation("Total jobs: {JobCount}", runPlan.Jobs.Count);
        
        var platformGroups = runPlan.Jobs.GroupBy(j => j.Platform);
        foreach (var platformGroup in platformGroups)
        {
            logger.LogInformation("Platform {Platform}: {JobCount} jobs", platformGroup.Key, platformGroup.Count());
            
            var deviceGroups = platformGroup.GroupBy(j => j.DeviceFolder);
            foreach (var deviceGroup in deviceGroups)
            {
                logger.LogInformation("  Device {Device}: {JobCount} jobs", deviceGroup.Key, deviceGroup.Count());
                
                var langGroups = deviceGroup.GroupBy(j => j.Language);
                foreach (var langGroup in langGroups)
                {
                    var screenshotCount = langGroup.Sum(j => j.Screenshots.Count);
                    logger.LogInformation("    Language {Language}: {ScreenshotCount} screenshots", 
                        langGroup.Key, screenshotCount);
                }
            }
        }

        if (runPlan.Jobs.Count > 0)
        {
            var firstJob = runPlan.Jobs[0];
            logger.LogInformation("Example output path: {OutputPath}", firstJob.OutputDirectory);
        }
    }
}