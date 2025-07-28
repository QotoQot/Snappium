using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snappium.Core.Abstractions;
using Snappium.Core.Appium;
using Snappium.Core.Build;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Snappium.Core.Infrastructure;
using Snappium.Core.Orchestration;
using Snappium.Core.Planning;

namespace Snappium.Cli.Commands;

public class RunCommand : Command
{
    private readonly IServiceProvider _serviceProvider;

    public RunCommand(IServiceProvider serviceProvider) : base("run", "Execute screenshot automation run")
    {
        _serviceProvider = serviceProvider;

        var configOption = new Option<FileInfo>(
            name: "--config",
            description: "Path to configuration JSON file")
        {
            IsRequired = true
        };

        var platformsOption = new Option<string[]>(
            name: "--platforms", 
            description: "Comma-separated platforms (ios,android)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var devicesOption = new Option<string[]>(
            name: "--devices",
            description: "Comma-separated device names")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var langsOption = new Option<string[]>(
            name: "--langs",
            description: "Comma-separated languages")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var screensOption = new Option<string[]>(
            name: "--screens",
            description: "Comma-separated screenshot names")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var buildOption = new Option<string>(
            name: "--build",
            description: "Build mode: auto|always|never")
        {
            ArgumentHelpName = "mode"
        };

        var noBuildOption = new Option<bool>(
            name: "--no-build",
            description: "Skip building apps");

        var iosAppOption = new Option<FileInfo>(
            name: "--ios-app",
            description: "Path to iOS .app bundle");

        var androidAppOption = new Option<FileInfo>(
            name: "--android-app", 
            description: "Path to Android .apk file");

        var outputOption = new Option<DirectoryInfo>(
            name: "--output",
            description: "Output directory for screenshots");

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
            description: "Enable verbose logging with debug output");

        AddOption(configOption);
        AddOption(platformsOption);
        AddOption(devicesOption);
        AddOption(langsOption);
        AddOption(screensOption);
        AddOption(buildOption);
        AddOption(noBuildOption);
        AddOption(iosAppOption);
        AddOption(androidAppOption);
        AddOption(outputOption);
        AddOption(basePortOption);
        AddOption(dryRunOption);
        AddOption(retryFailedOption);
        AddOption(verboseOption);

        this.SetHandler(async (context) =>
        {
            var config = context.ParseResult.GetValueForOption(configOption)!;
            var platforms = context.ParseResult.GetValueForOption(platformsOption);
            var devices = context.ParseResult.GetValueForOption(devicesOption);
            var langs = context.ParseResult.GetValueForOption(langsOption);
            var screens = context.ParseResult.GetValueForOption(screensOption);
            var build = context.ParseResult.GetValueForOption(buildOption);
            var noBuild = context.ParseResult.GetValueForOption(noBuildOption);
            var iosApp = context.ParseResult.GetValueForOption(iosAppOption);
            var androidApp = context.ParseResult.GetValueForOption(androidAppOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var basePort = context.ParseResult.GetValueForOption(basePortOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var retryFailed = context.ParseResult.GetValueForOption(retryFailedOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            context.ExitCode = await ExecuteAsync(config, platforms, devices, langs, screens, build, noBuild, 
                iosApp, androidApp, output, basePort, dryRun, retryFailed, verbose, context.GetCancellationToken());
        });
    }

    private async Task<int> ExecuteAsync(
        FileInfo configFile,
        string[]? platforms,
        string[]? devices,
        string[]? langs,
        string[]? screens,
        string? build,
        bool noBuild,
        FileInfo? iosApp,
        FileInfo? androidApp,
        DirectoryInfo? output,
        int? basePort,
        bool dryRun,
        FileInfo? retryFailed,
        bool verbose,
        CancellationToken cancellationToken)
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<RunCommand>>();
        
        // Create enhanced logger with verbose mode
        var snappiumLogger = new Snappium.Core.Logging.SnappiumConsoleLogger(logger, verbose);
        
        if (verbose)
        {
            snappiumLogger.LogDebug("Verbose mode enabled");
        }
        
        try
        {
            // Load configuration
            var configLoader = _serviceProvider.GetRequiredService<ConfigLoader>();
            var config = await configLoader.LoadAsync(configFile.FullName, schemaPath: null, cancellationToken);

            logger.LogInformation("Loaded configuration from {ConfigPath}", configFile.FullName);

            // Build CLI overrides
            var cliOverrides = new CliOverrides
            {
                IosAppPath = iosApp?.FullName,
                AndroidAppPath = androidApp?.FullName,
                OutputDirectory = output?.FullName,
                BasePort = basePort,
                NoBuild = noBuild,
                BuildConfiguration = build
            };

            // Create run plan
            var runPlanBuilder = _serviceProvider.GetRequiredService<RunPlanBuilder>();
            var portAllocator = _serviceProvider.GetRequiredService<PortAllocator>();
            
            // Use CLI override, config value, or default
            var configBasePort = config.Ports?.BasePort;
            var effectiveBasePort = basePort ?? configBasePort ?? 4723;
            var effectivePortOffset = config.Ports?.PortOffset ?? 10;
            
            
            if (basePort.HasValue || config.Ports?.BasePort.HasValue == true)
            {
                portAllocator = new PortAllocator(effectiveBasePort, effectivePortOffset);
            }

            var outputRoot = output?.FullName ?? Path.Combine(Environment.CurrentDirectory, "Screenshots");
            
            var runPlan = await runPlanBuilder.BuildAsync(
                config,
                outputRoot,
                platforms,
                devices,
                langs,
                screens,
                portAllocator,
                cancellationToken);

            logger.LogInformation("Built run plan with {JobCount} jobs", runPlan.Jobs.Count);

            if (dryRun)
            {
                PrintDryRunPlan(runPlan, logger);
                return 0;
            }

            // Execute the plan with enhanced logger
            var orchestrator = _serviceProvider.GetRequiredService<IOrchestrator>();
            
            // Update the orchestrator's logger if it supports enhanced logging
            if (orchestrator is Orchestrator concreteOrchestrator)
            {
                // Create a new orchestrator instance with the enhanced logger
                using var serviceScope = _serviceProvider.CreateScope();
                var enhancedOrchestrator = new Orchestrator(
                    serviceScope.ServiceProvider.GetRequiredService<IDriverFactory>(),
                    serviceScope.ServiceProvider.GetRequiredService<IActionExecutor>(),
                    serviceScope.ServiceProvider.GetRequiredService<IImageValidator>(),
                    serviceScope.ServiceProvider.GetRequiredService<IIosDeviceManager>(),
                    serviceScope.ServiceProvider.GetRequiredService<IAndroidDeviceManager>(),
                    serviceScope.ServiceProvider.GetRequiredService<IBuildService>(),
                    serviceScope.ServiceProvider.GetRequiredService<IAppiumServerController>(),
                    serviceScope.ServiceProvider.GetRequiredService<PortAllocator>(),
                    serviceScope.ServiceProvider.GetRequiredService<ILogger<Orchestrator>>(),
                    snappiumLogger);
                
                var result = await enhancedOrchestrator.ExecuteAsync(runPlan, config, cliOverrides, cancellationToken);
                
                // Write manifest
                var manifestWriter = _serviceProvider.GetRequiredService<IManifestWriter>();
                var manifestFiles = await manifestWriter.WriteAsync(result, outputRoot, cancellationToken);

                logger.LogInformation("Run completed. Success: {Success}", result.Success);
                logger.LogInformation("Manifest: {ManifestPath}", manifestFiles.ManifestJsonPath);
                logger.LogInformation("Summary: {SummaryPath}", manifestFiles.SummaryTextPath);

                return result.Success ? 0 : 1;
            }
            else
            {
                var result = await orchestrator.ExecuteAsync(runPlan, config, cliOverrides, cancellationToken);
                
                // Write manifest
                var manifestWriter = _serviceProvider.GetRequiredService<IManifestWriter>();
                var manifestFiles = await manifestWriter.WriteAsync(result, outputRoot, cancellationToken);

                logger.LogInformation("Run completed. Success: {Success}", result.Success);
                logger.LogInformation("Manifest: {ManifestPath}", manifestFiles.ManifestJsonPath);
                logger.LogInformation("Summary: {SummaryPath}", manifestFiles.SummaryTextPath);

                return result.Success ? 0 : 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Run failed with exception");
            return 1;
        }
    }

    private static void PrintDryRunPlan(RunPlan runPlan, ILogger logger)
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