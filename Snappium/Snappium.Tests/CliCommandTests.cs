using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Snappium.Cli.Commands;
using Snappium.Core.Appium;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Snappium.Core.Infrastructure;
using Snappium.Core.Orchestration;
using Snappium.Core.Planning;
using System.CommandLine;

namespace Snappium.Tests;

[TestFixture]
public class CliCommandTests
{
    IServiceProvider _serviceProvider = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        (_serviceProvider as IDisposable)?.Dispose();
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
        
        // Device management
        services.AddSingleton<IIosDeviceManager, IosDeviceManager>();
        services.AddSingleton<IAndroidDeviceManager, AndroidDeviceManager>();
        
        // Appium
        services.AddSingleton<IAppiumServerController, AppiumServerController>();
        services.AddSingleton<IDriverFactory, DriverFactory>();
        services.AddSingleton<IElementFinder, ElementFinder>();
        services.AddSingleton<IActionExecutor, ActionExecutor>();
        services.AddSingleton<IImageValidator, ImageValidator>();
        
        // Planning and orchestration
        services.AddSingleton<RunPlanBuilder>();
        services.AddSingleton<IOrchestrator, Orchestrator>();
        services.AddSingleton<IManifestWriter, ManifestWriter>();
        
        // Port allocator as scoped since it maintains state
        services.AddScoped<PortAllocator>(provider => new PortAllocator(Defaults.Ports.AppiumBasePort, Defaults.Ports.PortOffset));
    }

    [Test]
    public void RunCommand_CanBeCreated()
    {
        // Arrange & Act
        var command = new RunCommand(
            _serviceProvider.GetRequiredService<IOrchestrator>(),
            _serviceProvider.GetRequiredService<ConfigLoader>(),
            _serviceProvider.GetRequiredService<RunPlanBuilder>(),
            _serviceProvider.GetRequiredService<IManifestWriter>(),
            _serviceProvider.GetRequiredService<ILogger<RunCommand>>());

        // Assert
        Assert.That(command.Name, Is.EqualTo("run"));
        Assert.That(command.Description, Is.EqualTo("Execute screenshot automation run"));
    }

    [Test]
    public void ValidateConfigCommand_CanBeCreated()
    {
        // Arrange & Act
        var command = new ValidateConfigCommand(
            _serviceProvider.GetRequiredService<ConfigLoader>(),
            _serviceProvider.GetRequiredService<ILogger<ValidateConfigCommand>>());

        // Assert
        Assert.That(command.Name, Is.EqualTo("validate-config"));
        Assert.That(command.Description, Is.EqualTo("Validate configuration file"));
    }

    [Test]
    public void GenerateMatrixCommand_CanBeCreated()
    {
        // Arrange & Act
        var command = new GenerateMatrixCommand(
            _serviceProvider.GetRequiredService<ConfigLoader>(),
            _serviceProvider.GetRequiredService<RunPlanBuilder>(),
            _serviceProvider.GetRequiredService<ILogger<GenerateMatrixCommand>>());

        // Assert
        Assert.That(command.Name, Is.EqualTo("generate-matrix"));
        Assert.That(command.Description, Is.EqualTo("Generate CI matrix JSON from run plan"));
    }

    [Test]
    public async Task RunCommand_WithMissingConfig_ReturnsError()
    {
        // Arrange
        var command = new RunCommand(
            _serviceProvider.GetRequiredService<IOrchestrator>(),
            _serviceProvider.GetRequiredService<ConfigLoader>(),
            _serviceProvider.GetRequiredService<RunPlanBuilder>(),
            _serviceProvider.GetRequiredService<IManifestWriter>(),
            _serviceProvider.GetRequiredService<ILogger<RunCommand>>());
        var rootCommand = new RootCommand();
        rootCommand.Add(command);

        // Act
        var result = await rootCommand.InvokeAsync(new[] { "run", "--config", "nonexistent.json" });

        // Assert
        Assert.That(result, Is.Not.EqualTo(0));
    }

    [Test]
    public async Task ValidateConfigCommand_WithMissingConfig_ReturnsError()
    {
        // Arrange
        var command = new ValidateConfigCommand(
            _serviceProvider.GetRequiredService<ConfigLoader>(),
            _serviceProvider.GetRequiredService<ILogger<ValidateConfigCommand>>());
        var rootCommand = new RootCommand();
        rootCommand.Add(command);

        // Act
        var result = await rootCommand.InvokeAsync(new[] { "validate-config", "--config", "nonexistent.json" });

        // Assert
        Assert.That(result, Is.Not.EqualTo(0));
    }

    [Test]
    public async Task GenerateMatrixCommand_WithMissingConfig_ReturnsError()
    {
        // Arrange
        var command = new GenerateMatrixCommand(
            _serviceProvider.GetRequiredService<ConfigLoader>(),
            _serviceProvider.GetRequiredService<RunPlanBuilder>(),
            _serviceProvider.GetRequiredService<ILogger<GenerateMatrixCommand>>());
        var rootCommand = new RootCommand();
        rootCommand.Add(command);

        // Act
        var result = await rootCommand.InvokeAsync(new[] { "generate-matrix", "--config", "nonexistent.json" });

        // Assert
        Assert.That(result, Is.Not.EqualTo(0));
    }
}