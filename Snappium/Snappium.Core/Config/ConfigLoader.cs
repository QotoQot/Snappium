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

        // Validate device configurations
        await ValidateDeviceConfigurationsAsync(config, errors, cancellationToken);

        // Validate selector configurations
        await ValidateSelectorConfigurationsAsync(config, errors, cancellationToken);

        // Validate platform version formats
        await ValidatePlatformVersionsAsync(config, errors, cancellationToken);

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
    /// Validate device configurations for potential issues.
    /// </summary>
    private Task ValidateDeviceConfigurationsAsync(RootConfig config, List<string> errors, CancellationToken cancellationToken)
    {
        // Validate iOS device names don't contain problematic characters
        foreach (var device in config.Devices.Ios)
        {
            if (device.Name.Contains("\"") || device.Name.Contains("'"))
            {
                errors.Add($"iOS device name '{device.Name}' contains quotes which may cause command execution issues");
            }
            
            if (device.Name.Length > 100)
            {
                errors.Add($"iOS device name '{device.Name}' is too long (>100 characters)");
            }
            
            // Check for common iOS simulator naming patterns
            if (!device.Name.Contains("iPhone") && !device.Name.Contains("iPad") && !device.Name.Contains("Apple"))
            {
                _logger.LogWarning("iOS device name '{DeviceName}' doesn't follow typical simulator naming pattern", device.Name);
            }
        }

        // Validate Android device configurations
        foreach (var device in config.Devices.Android)
        {
            if (string.IsNullOrWhiteSpace(device.Avd))
            {
                errors.Add($"Android device '{device.Name}' has empty AVD name");
            }
            
            if (device.Avd.Contains(" "))
            {
                errors.Add($"Android AVD name '{device.Avd}' contains spaces which may cause emulator issues");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validate selector configurations to ensure they use supported strategies.
    /// </summary>
    private Task ValidateSelectorConfigurationsAsync(RootConfig config, List<string> errors, CancellationToken cancellationToken)
    {
        foreach (var screenshot in config.Screenshots)
        {
            foreach (var action in screenshot.Actions)
            {
                // Check wait_for actions
                if (action.WaitFor != null)
                {
                    ValidateSelector(action.WaitFor.Selector, $"Screenshot '{screenshot.Name}' wait_for action", errors);
                }
                
                // Check tap actions
                if (action.Tap != null)
                {
                    ValidateSelector(action.Tap, $"Screenshot '{screenshot.Name}' tap action", errors);
                }
            }
            
            // Check assert selectors
            if (screenshot.Assert?.Ios != null)
            {
                ValidateSelector(screenshot.Assert.Ios, $"Screenshot '{screenshot.Name}' iOS assert", errors);
            }
            if (screenshot.Assert?.Android != null)
            {
                ValidateSelector(screenshot.Assert.Android, $"Screenshot '{screenshot.Name}' Android assert", errors);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Validate individual selector to ensure it has at least one supported strategy.
    /// </summary>
    private static void ValidateSelector(Selector selector, string context, List<string> errors)
    {
        var hasValidStrategy = !string.IsNullOrEmpty(selector.AccessibilityId) ||
                              !string.IsNullOrEmpty(selector.Id) ||
                              !string.IsNullOrEmpty(selector.IosClassChain) ||
                              !string.IsNullOrEmpty(selector.AndroidUiautomator) ||
                              !string.IsNullOrEmpty(selector.Xpath);

        if (!hasValidStrategy)
        {
            errors.Add($"{context} has selector with no valid locator strategy (AccessibilityId, Id, IosClassChain, AndroidUiautomator, or Xpath required)");
        }

        // Warn about XPath usage (discouraged but allowed)
        if (!string.IsNullOrEmpty(selector.Xpath))
        {
            // Simple validation for common XPath patterns
            if (!selector.Xpath.StartsWith("/") && !selector.Xpath.StartsWith("("))
            {
                errors.Add($"{context} has invalid XPath selector '{selector.Xpath}' - should start with '/' or '('");
            }
        }
    }

    /// <summary>
    /// Validate platform version formats.
    /// </summary>
    private Task ValidatePlatformVersionsAsync(RootConfig config, List<string> errors, CancellationToken cancellationToken)
    {
        // Validate iOS platform versions (should be like "18.5", "17.0", etc.)
        foreach (var device in config.Devices.Ios)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(device.PlatformVersion, @"^\d+\.\d+$"))
            {
                errors.Add($"iOS device '{device.Name}' has invalid platform version format '{device.PlatformVersion}' - should be like '18.5'");
            }
            
            if (device.PlatformVersion.StartsWith("0"))
            {
                errors.Add($"iOS device '{device.Name}' has platform version '{device.PlatformVersion}' starting with 0");
            }
        }

        // Validate Android platform versions (should be like "34", "33", etc.)
        foreach (var device in config.Devices.Android)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(device.PlatformVersion, @"^\d+$"))
            {
                errors.Add($"Android device '{device.Name}' has invalid platform version format '{device.PlatformVersion}' - should be a number like '34'");
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
            Artifacts = new Artifacts
            {
                Ios = new IosArtifact
                {
                    ArtifactGlob = dto.Artifacts.Ios.ArtifactGlob,
                    Package = dto.Artifacts.Ios.Package
                },
                Android = new AndroidArtifact
                {
                    ArtifactGlob = dto.Artifacts.Android.ArtifactGlob,
                    Package = dto.Artifacts.Android.Package
                }
            },
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
    public required ArtifactsDto Artifacts { get; set; }
    public TimeoutsDto? Timeouts { get; set; }
    public PortsDto? Ports { get; set; }
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

internal sealed class ArtifactsDto
{
    public required IosArtifactDto Ios { get; set; }
    public required AndroidArtifactDto Android { get; set; }
}

internal sealed class IosArtifactDto
{
    public required string ArtifactGlob { get; set; }
    public required string Package { get; set; }
}

internal sealed class AndroidArtifactDto
{
    public required string ArtifactGlob { get; set; }
    public required string Package { get; set; }
}