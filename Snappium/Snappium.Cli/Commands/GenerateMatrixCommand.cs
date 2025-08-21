using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snappium.Core.Config;
using Snappium.Core.Planning;

namespace Snappium.Cli.Commands;

public class GenerateMatrixCommand : Command
{
    private readonly ConfigLoader _configLoader;
    private readonly RunPlanBuilder _runPlanBuilder;
    private readonly ILogger<GenerateMatrixCommand> _logger;
    private readonly FilteringOptions _filteringOptions;

    public GenerateMatrixCommand(ConfigLoader configLoader, RunPlanBuilder runPlanBuilder, ILogger<GenerateMatrixCommand> logger) : base("generate-matrix", "Generate CI matrix JSON from run plan")
    {
        _configLoader = configLoader;
        _runPlanBuilder = runPlanBuilder;
        _logger = logger;

        // Add shared filtering options
        _filteringOptions = SharedCommandOptions.AddFilteringOptions(this);

        // Add command-specific options
        var formatOption = new Option<string>(
            name: "--format",
            description: "Output format: github|gitlab|azure")
        {
            ArgumentHelpName = "format"
        };

        AddOption(formatOption);

        this.SetHandler(async (context) =>
        {
            var config = context.ParseResult.GetValueForOption(_filteringOptions.Config)!;
            var platforms = context.ParseResult.GetValueForOption(_filteringOptions.Platforms);
            var devices = context.ParseResult.GetValueForOption(_filteringOptions.Devices);
            var langs = context.ParseResult.GetValueForOption(_filteringOptions.Languages);
            var screens = context.ParseResult.GetValueForOption(_filteringOptions.Screenshots);
            var output = context.ParseResult.GetValueForOption(_filteringOptions.Output);
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "github";

            context.ExitCode = await ExecuteAsync(config, platforms, devices, langs, screens, output, format, context.GetCancellationToken());
        });
    }

    private async Task<int> ExecuteAsync(
        FileInfo configFile,
        string[]? platforms,
        string[]? devices,
        string[]? langs,
        string[]? screens,
        DirectoryInfo? output,
        string format,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load configuration
            var config = await _configLoader.LoadAsync(configFile.FullName, schemaPath: null, cancellationToken);
            _logger.LogInformation("Loaded configuration from {ConfigPath}", configFile.FullName);

            // Create run plan with port allocation
            var configBasePort = config.Ports?.BasePort ?? Defaults.Ports.AppiumBasePort;
            var effectivePortOffset = config.Ports?.PortOffset ?? Defaults.Ports.PortOffset;
            var portAllocator = new PortAllocator(configBasePort, effectivePortOffset);
            
            var outputRoot = output?.FullName ?? Path.Combine(Environment.CurrentDirectory, "Screenshots");
            
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

            // Generate matrix based on format
            var matrix = format.ToLower() switch
            {
                "github" => GenerateGitHubMatrix(runPlan),
                "gitlab" => GenerateGitLabMatrix(runPlan),
                "azure" => GenerateAzureMatrix(runPlan),
                _ => throw new ArgumentException($"Unknown format: {format}")
            };

            // Output to console
            var json = JsonSerializer.Serialize(matrix, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Console.WriteLine(json);

            _logger.LogInformation("Generated {Format} matrix with {JobCount} jobs", format, runPlan.Jobs.Count);

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Matrix generation failed");
            return 1;
        }
    }

    private static object GenerateGitHubMatrix(RunPlan runPlan)
    {
        var include = runPlan.Jobs.Select((job, index) => new
        {
            job_id = $"job-{index}",
            platform = job.Platform.ToString().ToLower(),
            device = job.DeviceFolder,
            language = job.Language,
            screenshots = job.Screenshots.Count,
            output_dir = job.OutputDirectory
        }).ToList();

        return new
        {
            include
        };
    }

    private static object GenerateGitLabMatrix(RunPlan runPlan)
    {
        var variables = new Dictionary<string, object>();
        
        for (int i = 0; i < runPlan.Jobs.Count; i++)
        {
            var job = runPlan.Jobs[i];
            var jobKey = $"JOB_{i}";
            
            variables[jobKey] = new
            {
                PLATFORM = job.Platform.ToString().ToLower(),
                DEVICE = job.DeviceFolder,
                LANGUAGE = job.Language,
                OUTPUT_DIR = job.OutputDirectory
            };
        }

        return variables;
    }

    private static object GenerateAzureMatrix(RunPlan runPlan)
    {
        var strategy = new Dictionary<string, object>();
        
        var matrix = new Dictionary<string, object>();
        
        for (int i = 0; i < runPlan.Jobs.Count; i++)
        {
            var job = runPlan.Jobs[i];
            var jobKey = $"job_{i}";
            
            matrix[jobKey] = new
            {
                platform = job.Platform.ToString().ToLower(),
                device = job.DeviceFolder,
                language = job.Language,
                outputDir = job.OutputDirectory
            };
        }

        strategy["matrix"] = matrix;
        
        return new
        {
            strategy
        };
    }
}