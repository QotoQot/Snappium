using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snappium.Cli.Commands;
using Snappium.Core.Appium;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Snappium.Core.Infrastructure;
using Snappium.Core.Logging;
using Snappium.Core.Orchestration;
using Snappium.Core.Planning;

namespace Snappium.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);

        await using var serviceProvider = services.BuildServiceProvider();
        
        // Setup graceful shutdown handler to prevent zombie processes
        var processManager = serviceProvider.GetRequiredService<ProcessManager>();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            Console.WriteLine("\nCancellation requested. Shutting down managed processes...");
            try
            {
                // Perform synchronous cleanup - ProcessManager.Dispose() handles async cleanup internally
                processManager.Dispose();
                Console.WriteLine("Process cleanup completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error during process cleanup: {ex.Message}");
            }
            eventArgs.Cancel = false; // Allow the process to terminate
        };
        
        var rootCommand = new RootCommand("Snappium - Screenshot automation for mobile apps")
        {
            new RunCommand(
                serviceProvider.GetRequiredService<IOrchestrator>(),
                serviceProvider.GetRequiredService<ConfigLoader>(),
                serviceProvider.GetRequiredService<RunPlanBuilder>(),
                serviceProvider.GetRequiredService<IManifestWriter>(),
                serviceProvider.GetRequiredService<ILogger<RunCommand>>()),
            new ValidateConfigCommand(
                serviceProvider.GetRequiredService<ConfigLoader>(),
                serviceProvider.GetRequiredService<ILogger<ValidateConfigCommand>>()),
            new GenerateMatrixCommand(
                serviceProvider.GetRequiredService<ConfigLoader>(),
                serviceProvider.GetRequiredService<RunPlanBuilder>(),
                serviceProvider.GetRequiredService<ILogger<GenerateMatrixCommand>>())
        };

        return await rootCommand.InvokeAsync(args);
    }

    static void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core services
        services.AddSingleton<ICommandRunner, CommandRunner>();
        services.AddSingleton<ConfigLoader>();
        
        // Enhanced logging - will be configured per-command based on verbose flag
        services.AddSingleton<ISnappiumLogger>(provider => 
        {
            var logger = provider.GetRequiredService<ILogger<SnappiumConsoleLogger>>();
            return new SnappiumConsoleLogger(logger, verboseMode: false); // Default to non-verbose
        });
        
        // Device management
        services.AddSingleton<IIosDeviceManager, IosDeviceManager>();
        services.AddSingleton<IAndroidDeviceManager, AndroidDeviceManager>();
        
        // Appium
        services.AddSingleton<IAppiumServerController, AppiumServerController>();
        services.AddSingleton<IDriverFactory, DriverFactory>();
        services.AddSingleton<IElementFinder, ElementFinder>();
        services.AddSingleton<IActionExecutor, ActionExecutor>();
        services.AddSingleton<IImageValidator, ImageValidator>();
        
        // Process management for cleanup
        services.AddSingleton<ProcessManager>();
        
        // Planning and orchestration
        services.AddSingleton<RunPlanBuilder>();
        services.AddScoped<IJobExecutor>(provider => new JobExecutor(
            provider.GetRequiredService<IDriverFactory>(),
            provider.GetRequiredService<IActionExecutor>(),
            provider.GetRequiredService<IImageValidator>(),
            provider.GetRequiredService<IIosDeviceManager>(),
            provider.GetRequiredService<IAndroidDeviceManager>(),
            provider.GetRequiredService<IAppiumServerController>(),
            provider.GetRequiredService<ProcessManager>(),
            provider, // Pass the service provider itself
            provider.GetRequiredService<ILogger<JobExecutor>>(),
            provider.GetService<ISnappiumLogger>()
        )); // Scoped to enable parallel job isolation
        services.AddSingleton<IOrchestrator, Orchestrator>();
        services.AddSingleton<IManifestWriter, ManifestWriter>();
        
        // Note: PortAllocator is now created locally in each command with appropriate configuration
    }
}