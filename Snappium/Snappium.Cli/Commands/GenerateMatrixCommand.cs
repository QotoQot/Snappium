using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snappium.Core.Config;
using Snappium.Core.Planning;

namespace Snappium.Cli.Commands;

public class GenerateMatrixCommand : Command
{
    private readonly IServiceProvider _serviceProvider;

    public GenerateMatrixCommand(IServiceProvider serviceProvider) : base("generate-matrix", "Generate CI matrix JSON from run plan")
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

        var outputOption = new Option<DirectoryInfo>(
            name: "--output",
            description: "Output directory for screenshots");

        var formatOption = new Option<string>(
            name: "--format",
            description: "Output format: github|gitlab|azure")
        {
            ArgumentHelpName = "format"
        };

        AddOption(configOption);
        AddOption(platformsOption);
        AddOption(devicesOption);
        AddOption(langsOption);
        AddOption(screensOption);
        AddOption(outputOption);
        AddOption(formatOption);

        this.SetHandler(async (context) =>
        {
            var config = context.ParseResult.GetValueForOption(configOption)!;
            var platforms = context.ParseResult.GetValueForOption(platformsOption);
            var devices = context.ParseResult.GetValueForOption(devicesOption);
            var langs = context.ParseResult.GetValueForOption(langsOption);
            var screens = context.ParseResult.GetValueForOption(screensOption);
            var output = context.ParseResult.GetValueForOption(outputOption);
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
        var logger = _serviceProvider.GetRequiredService<ILogger<GenerateMatrixCommand>>();
        
        try
        {
            // Load configuration
            var configLoader = _serviceProvider.GetRequiredService<ConfigLoader>();
            var config = await configLoader.LoadAsync(configFile.FullName, schemaPath: null, cancellationToken);

            logger.LogInformation("Loaded configuration from {ConfigPath}", configFile.FullName);

            // Create run plan
            var runPlanBuilder = _serviceProvider.GetRequiredService<RunPlanBuilder>();
            var portAllocator = _serviceProvider.GetRequiredService<PortAllocator>();
            
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

            logger.LogInformation("Generated {Format} matrix with {JobCount} jobs", format, runPlan.Jobs.Count);

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Matrix generation failed");
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