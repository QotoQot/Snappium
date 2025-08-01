using System.Text.Json;
using NJsonSchema;
using Microsoft.Extensions.Logging;

namespace Snappium.Core.Config;

/// <summary>
/// Loads and validates screenshot automation configuration files.
/// </summary>
public sealed class ConfigLoader
{
    private readonly ILogger<ConfigLoader> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigLoader(ILogger<ConfigLoader> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        // Add custom converter for ScreenshotAction polymorphic deserialization 
        _jsonOptions.Converters.Add(new ScreenshotActionJsonConverter());
    }

    /// <summary>
    /// Load and validate configuration from a JSON file.
    /// </summary>
    /// <param name="configPath">Path to the configuration JSON file</param>
    /// <param name="schemaPath">Path to the JSON schema file (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validated configuration object</returns>
    public async Task<RootConfig> LoadAsync(string configPath, string? schemaPath = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading configuration from {ConfigPath}", configPath);

        // Read config file
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Configuration file '{configPath}' not found");
        }

        var configJson = await File.ReadAllTextAsync(configPath, cancellationToken);
        
        // Validate against schema if provided
        if (!string.IsNullOrEmpty(schemaPath))
        {
            await ValidateSchemaAsync(configJson, schemaPath, cancellationToken);
        }

        // Deserialize to typed objects
        var config = JsonSerializer.Deserialize<RootConfigDto>(configJson, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize configuration");

        // Convert DTO to domain model
        var rootConfig = ConvertFromDto(config);

        // Perform semantic validation
        await ValidateSemanticAsync(rootConfig, cancellationToken);

        _logger.LogInformation("Configuration loaded and validated successfully");
        return rootConfig;
    }

    /// <summary>
    /// Validate JSON against schema.
    /// </summary>
    private async Task ValidateSchemaAsync(string configJson, string schemaPath, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Validating configuration against schema {SchemaPath}", schemaPath);

        if (!File.Exists(schemaPath))
        {
            _logger.LogWarning("Schema file '{SchemaPath}' not found - skipping schema validation", schemaPath);
            return;
        }

        var schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken);
        var schema = await JsonSchema.FromJsonAsync(schemaJson, cancellationToken);
        
        var errors = schema.Validate(configJson);
        if (errors.Count > 0)
        {
            var errorMessages = errors.Select(e => $"  • {e.Path}: {e.Kind} - {e.Property}").ToList();
            var message = $"Configuration validation failed:\n{string.Join("\n", errorMessages)}";
            throw new InvalidOperationException(message);
        }

        _logger.LogDebug("Schema validation passed");
    }

    /// <summary>
    /// Perform semantic validation on the configuration.
    /// </summary>
    private async Task ValidateSemanticAsync(RootConfig config, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Performing semantic validation");

        var errors = new List<string>();

        // Validate locale mappings
        await ValidateLocaleMappingsAsync(config, errors, cancellationToken);

        // Validate device folder uniqueness
        await ValidateDeviceFoldersAsync(config, errors, cancellationToken);

        // Validate screenshot actions
        await ValidateScreenshotActionsAsync(config, errors, cancellationToken);

        if (errors.Count > 0)
        {
            var message = $"Semantic validation failed:\n{string.Join("\n", errors.Select(e => $"  • {e}"))}";
            throw new InvalidOperationException(message);
        }

        _logger.LogDebug("Semantic validation passed");
    }

    /// <summary>
    /// Validate that all languages have locale mappings for both platforms.
    /// </summary>
    private Task ValidateLocaleMappingsAsync(RootConfig config, List<string> errors, CancellationToken cancellationToken)
    {
        var missingLocales = config.Languages
            .Where(lang => !config.LocaleMapping.ContainsKey(lang))
            .ToList();

        if (missingLocales.Count > 0)
        {
            errors.Add($"Languages missing from locale_mapping: [{string.Join(", ", missingLocales)}]");
        }

        foreach (var (lang, mapping) in config.LocaleMapping)
        {
            if (string.IsNullOrEmpty(mapping.Ios))
            {
                errors.Add($"Language '{lang}' missing iOS locale mapping");
            }
            if (string.IsNullOrEmpty(mapping.Android))
            {
                errors.Add($"Language '{lang}' missing Android locale mapping");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validate that device folders are unique across platforms.
    /// </summary>
    private Task ValidateDeviceFoldersAsync(RootConfig config, List<string> errors, CancellationToken cancellationToken)
    {
        var allFolders = config.Devices.Ios.Select(d => d.Folder)
            .Concat(config.Devices.Android.Select(d => d.Folder))
            .ToList();

        var duplicates = allFolders
            .GroupBy(f => f)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            errors.Add($"Device folders must be unique across platforms. Duplicates: [{string.Join(", ", duplicates)}]");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validate screenshot action configurations.
    /// </summary>
    private Task ValidateScreenshotActionsAsync(RootConfig config, List<string> errors, CancellationToken cancellationToken)
    {
        foreach (var screenshot in config.Screenshots)
        {
            var hasCapture = screenshot.Actions.Any(a => a.Capture != null);
            if (!hasCapture)
            {
                _logger.LogWarning("Screenshot '{ScreenshotName}' has no capture action", screenshot.Name);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Convert DTO to domain model.
    /// </summary>
    private static RootConfig ConvertFromDto(RootConfigDto dto)
    {
        return new RootConfig
        {
            Devices = new Devices
            {
                Ios = dto.Devices.Ios.Select(d => new IosDevice
                {
                    Name = d.Name,
                    Udid = d.Udid,
                    Folder = d.Folder,
                    PlatformVersion = d.PlatformVersion
                }).ToList(),
                Android = dto.Devices.Android.Select(d => new AndroidDevice
                {
                    Name = d.Name,
                    Avd = d.Avd,
                    Folder = d.Folder,
                    PlatformVersion = d.PlatformVersion
                }).ToList()
            },
            Languages = dto.Languages,
            LocaleMapping = dto.LocaleMapping.ToDictionary(
                kvp => kvp.Key,
                kvp => new LocaleMapping { Ios = kvp.Value.Ios, Android = kvp.Value.Android }
            ),
            Screenshots = dto.Screenshots.Select(ConvertScreenshotFromDto).ToList(),
            BuildConfig = dto.BuildConfig != null ? new BuildConfig
            {
                Ios = dto.BuildConfig.Ios != null ? new PlatformBuildConfig
                {
                    Csproj = dto.BuildConfig.Ios.Csproj,
                    Tfm = dto.BuildConfig.Ios.Tfm,
                    ArtifactGlob = dto.BuildConfig.Ios.ArtifactGlob,
                    Package = dto.BuildConfig.Ios.Package
                } : null,
                Android = dto.BuildConfig.Android != null ? new PlatformBuildConfig
                {
                    Csproj = dto.BuildConfig.Android.Csproj,
                    Tfm = dto.BuildConfig.Android.Tfm,
                    ArtifactGlob = dto.BuildConfig.Android.ArtifactGlob,
                    Package = dto.BuildConfig.Android.Package
                } : null
            } : null,
            Timeouts = dto.Timeouts != null ? new Timeouts
            {
                DefaultWaitMs = dto.Timeouts.DefaultWaitMs,
                ImplicitWaitMs = dto.Timeouts.ImplicitWaitMs,
                PageLoadTimeoutMs = dto.Timeouts.PageLoadTimeoutMs
            } : null,
            Ports = dto.Ports != null ? new Ports
            {
                BasePort = dto.Ports.BasePort,
                PortOffset = dto.Ports.PortOffset
            } : null,
            AppReset = dto.AppReset != null ? new AppReset
            {
                Policy = dto.AppReset.Policy,
                ClearDataOnLanguageChange = dto.AppReset.ClearDataOnLanguageChange,
                ReinstallVsRelaunch = dto.AppReset.ReinstallVsRelaunch
            } : null,
            FailureArtifacts = dto.FailureArtifacts != null ? new FailureArtifacts
            {
                SavePageSource = dto.FailureArtifacts.SavePageSource,
                SaveScreenshot = dto.FailureArtifacts.SaveScreenshot,
                SaveAppiumLogs = dto.FailureArtifacts.SaveAppiumLogs,
                SaveDeviceLogs = dto.FailureArtifacts.SaveDeviceLogs,
                ArtifactsDir = dto.FailureArtifacts.ArtifactsDir
            } : null,
            StatusBar = dto.StatusBar != null ? new StatusBar
            {
                Ios = dto.StatusBar.Ios != null ? new IosStatusBar
                {
                    Time = dto.StatusBar.Ios.Time,
                    WifiBars = dto.StatusBar.Ios.WifiBars,
                    CellularBars = dto.StatusBar.Ios.CellularBars,
                    BatteryState = dto.StatusBar.Ios.BatteryState
                } : null,
                Android = dto.StatusBar.Android != null ? new AndroidStatusBar
                {
                    DemoMode = dto.StatusBar.Android.DemoMode,
                    Clock = dto.StatusBar.Android.Clock,
                    Battery = dto.StatusBar.Android.Battery,
                    Wifi = dto.StatusBar.Android.Wifi,
                    Notifications = dto.StatusBar.Android.Notifications
                } : null
            } : null,
            Validation = dto.Validation != null ? new Validation
            {
                EnforceImageSize = dto.Validation.EnforceImageSize,
                ExpectedSizes = dto.Validation.ExpectedSizes != null ? new ExpectedSizes
                {
                    Ios = dto.Validation.ExpectedSizes.Ios?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new DeviceSize
                        {
                            Portrait = kvp.Value.Portrait,
                            Landscape = kvp.Value.Landscape
                        }
                    ),
                    Android = dto.Validation.ExpectedSizes.Android?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new DeviceSize
                        {
                            Portrait = kvp.Value.Portrait,
                            Landscape = kvp.Value.Landscape
                        }
                    )
                } : null
            } : null,
            Capabilities = dto.Capabilities != null ? new Capabilities
            {
                Ios = dto.Capabilities.Ios,
                Android = dto.Capabilities.Android
            } : null,
            Dismissors = dto.Dismissors != null ? new Dismissors
            {
                Ios = dto.Dismissors.Ios?.Select(ConvertSelectorFromDto).ToList(),
                Android = dto.Dismissors.Android?.Select(ConvertSelectorFromDto).ToList()
            } : null
        };
    }

    private static ScreenshotPlan ConvertScreenshotFromDto(ScreenshotPlanDto dto)
    {
        return new ScreenshotPlan
        {
            Name = dto.Name,
            Orientation = dto.Orientation,
            Actions = dto.Actions, // Direct assignment - converter handles deserialization
            Assert = dto.Assert != null ? new PlatformAssertions
            {
                Ios = dto.Assert.TryGetValue("ios", out var iosAssert) ? ConvertSelectorFromDto(iosAssert) : null,
                Android = dto.Assert.TryGetValue("android", out var androidAssert) ? ConvertSelectorFromDto(androidAssert) : null
            } : null
        };
    }


    private static Selector ConvertSelectorFromDto(SelectorDto dto)
    {
        return new Selector
        {
            AccessibilityId = dto.AccessibilityId,
            IosClassChain = dto.ClassChain,
            AndroidUiautomator = dto.UiAutomator,
            Xpath = dto.XPath,
            Id = dto.Id
        };
    }
}

// DTOs for JSON deserialization
internal sealed class RootConfigDto
{
    public required DevicesDto Devices { get; set; }
    public required List<string> Languages { get; set; }
    public required Dictionary<string, LocaleMappingDto> LocaleMapping { get; set; }
    public required List<ScreenshotPlanDto> Screenshots { get; set; }
    public BuildConfigDto? BuildConfig { get; set; }
    public TimeoutsDto? Timeouts { get; set; }
    public PortsDto? Ports { get; set; }
    public AppResetDto? AppReset { get; set; }
    public FailureArtifactsDto? FailureArtifacts { get; set; }
    public StatusBarDto? StatusBar { get; set; }
    public ValidationDto? Validation { get; set; }
    public CapabilitiesDto? Capabilities { get; set; }
    public DismissorsDto? Dismissors { get; set; }
}

internal sealed class DevicesDto
{
    public required List<IosDeviceDto> Ios { get; set; }
    public required List<AndroidDeviceDto> Android { get; set; }
}

internal sealed class IosDeviceDto
{
    public required string Name { get; set; }
    public string? Udid { get; set; }
    public required string Folder { get; set; }
    public required string PlatformVersion { get; set; }
}

internal sealed class AndroidDeviceDto
{
    public required string Name { get; set; }
    public required string Avd { get; set; }
    public required string Folder { get; set; }
    public required string PlatformVersion { get; set; }
}

internal sealed class LocaleMappingDto
{
    public required string Ios { get; set; }
    public required string Android { get; set; }
}

internal sealed class ScreenshotPlanDto
{
    public required string Name { get; set; }
    public string? Orientation { get; set; }
    public required List<ScreenshotAction> Actions { get; set; }
    public Dictionary<string, SelectorDto>? Assert { get; set; }
}

internal sealed class SelectorDto
{
    public string? AccessibilityId { get; set; }
    public string? ClassChain { get; set; }
    public string? UiAutomator { get; set; }
    public string? XPath { get; set; }
    public string? Id { get; set; }
}

internal sealed class BuildConfigDto
{
    public PlatformBuildConfigDto? Ios { get; set; }
    public PlatformBuildConfigDto? Android { get; set; }
}

internal sealed class PlatformBuildConfigDto
{
    public string? Csproj { get; set; }
    public string? Tfm { get; set; }
    public string? ArtifactGlob { get; set; }
    public string? Package { get; set; }
}

internal sealed class TimeoutsDto
{
    public int? DefaultWaitMs { get; set; }
    public int? ImplicitWaitMs { get; set; }
    public int? PageLoadTimeoutMs { get; set; }
}

internal sealed class PortsDto
{
    public int? BasePort { get; set; }
    public int? PortOffset { get; set; }
}

internal sealed class AppResetDto
{
    public string? Policy { get; set; }
    public bool? ClearDataOnLanguageChange { get; set; }
    public string? ReinstallVsRelaunch { get; set; }
}

internal sealed class FailureArtifactsDto
{
    public bool? SavePageSource { get; set; }
    public bool? SaveScreenshot { get; set; }
    public bool? SaveAppiumLogs { get; set; }
    public bool? SaveDeviceLogs { get; set; }
    public string? ArtifactsDir { get; set; }
}

internal sealed class StatusBarDto
{
    public IosStatusBarDto? Ios { get; set; }
    public AndroidStatusBarDto? Android { get; set; }
}

internal sealed class IosStatusBarDto
{
    public string? Time { get; set; }
    public int? WifiBars { get; set; }
    public int? CellularBars { get; set; }
    public string? BatteryState { get; set; }
}

internal sealed class AndroidStatusBarDto
{
    public bool? DemoMode { get; set; }
    public string? Clock { get; set; }
    public int? Battery { get; set; }
    public string? Wifi { get; set; }
    public string? Notifications { get; set; }
}

internal sealed class ValidationDto
{
    public bool? EnforceImageSize { get; set; }
    public ExpectedSizesDto? ExpectedSizes { get; set; }
}

internal sealed class ExpectedSizesDto
{
    public Dictionary<string, DeviceSizeDto>? Ios { get; set; }
    public Dictionary<string, DeviceSizeDto>? Android { get; set; }
}

internal sealed class DeviceSizeDto
{
    public int[]? Portrait { get; set; }
    public int[]? Landscape { get; set; }
}

internal sealed class CapabilitiesDto
{
    public Dictionary<string, object>? Ios { get; set; }
    public Dictionary<string, object>? Android { get; set; }
}

internal sealed class DismissorsDto
{
    public List<SelectorDto>? Ios { get; set; }
    public List<SelectorDto>? Android { get; set; }
}