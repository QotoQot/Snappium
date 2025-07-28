using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snappium.Cli.Commands;
using Snappium.Core.Appium;
using Snappium.Core.Build;
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
        
        using var serviceProvider = services.BuildServiceProvider();
        
        var rootCommand = new RootCommand("Snappium - Screenshot automation for mobile apps")
        {
            new RunCommand(serviceProvider),
            new ValidateConfigCommand(serviceProvider),
            new GenerateMatrixCommand(serviceProvider)
        };

        return await rootCommand.InvokeAsync(args);
    }

    private static void ConfigureServices(IServiceCollection services)
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
        
        // Build and Appium
        services.AddSingleton<IBuildService, BuildService>();
        services.AddSingleton<IAppiumServerController, AppiumServerController>();
        services.AddSingleton<IDriverFactory, DriverFactory>();
        services.AddSingleton<IElementFinder, ElementFinder>();
        services.AddSingleton<IActionExecutor, ActionExecutor>();
        services.AddSingleton<IImageValidator, ImageValidator>();
        
        // Planning and orchestration
        services.AddSingleton<RunPlanBuilder>();
        services.AddScoped<IJobExecutor, JobExecutor>(); // Scoped to enable parallel job isolation
        services.AddSingleton<IOrchestrator, Orchestrator>();
        services.AddSingleton<IManifestWriter, ManifestWriter>();
        
        // Port allocator as scoped since it maintains state
        services.AddScoped<PortAllocator>(provider => new PortAllocator(4723, 10));
    }
}