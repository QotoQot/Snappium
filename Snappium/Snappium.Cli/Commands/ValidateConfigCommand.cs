using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snappium.Core.Config;

namespace Snappium.Cli.Commands;

public class ValidateConfigCommand : Command
{
    private readonly ConfigLoader _configLoader;
    private readonly ILogger<ValidateConfigCommand> _logger;

    public ValidateConfigCommand(ConfigLoader configLoader, ILogger<ValidateConfigCommand> logger) : base("validate-config", "Validate configuration file")
    {
        _configLoader = configLoader;
        _logger = logger;

        var configOption = new Option<FileInfo>(
            name: "--config",
            description: "Path to configuration JSON file")
        {
            IsRequired = true
        };

        var schemaOption = new Option<FileInfo>(
            name: "--schema",
            description: "Path to JSON schema file for validation");

        AddOption(configOption);
        AddOption(schemaOption);

        this.SetHandler(async (context) =>
        {
            var config = context.ParseResult.GetValueForOption(configOption)!;
            var schema = context.ParseResult.GetValueForOption(schemaOption);

            context.ExitCode = await ExecuteAsync(config, schema, context.GetCancellationToken());
        });
    }

    private async Task<int> ExecuteAsync(
        FileInfo configFile,
        FileInfo? schemaFile,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!configFile.Exists)
            {
                _logger.LogError("Configuration file not found: {ConfigPath}", configFile.FullName);
                return 1;
            }

            // Load and validate configuration
            var config = await _configLoader.LoadAsync(configFile.FullName, schemaFile?.FullName, cancellationToken);

            _logger.LogInformation("Configuration loaded successfully from {ConfigPath}", configFile.FullName);

            // Basic validation checks
            var validationErrors = new List<string>();

            // Check required sections
            if (config.Devices == null)
            {
                validationErrors.Add("Missing 'devices' section");
            }
            else
            {
                if ((config.Devices.Ios?.Count ?? 0) == 0 && (config.Devices.Android?.Count ?? 0) == 0)
                {
                    validationErrors.Add("No devices configured in 'devices' section");
                }
            }

            if (config.Languages == null || config.Languages.Count == 0)
            {
                validationErrors.Add("Missing or empty 'languages' array");
            }

            if (config.Screenshots == null || config.Screenshots.Count == 0)
            {
                validationErrors.Add("Missing or empty 'screenshots' array");
            }

            // Check locale mappings
            if (config.LocaleMapping != null && config.Languages != null)
            {
                foreach (var lang in config.Languages)
                {
                    if (!config.LocaleMapping.ContainsKey(lang))
                    {
                        validationErrors.Add($"Missing locale mapping for language '{lang}'");
                    }
                }
            }

            // Schema validation if provided
            if (schemaFile != null)
            {
                if (!schemaFile.Exists)
                {
                    _logger.LogError("Schema file not found: {SchemaPath}", schemaFile.FullName);
                    return 1;
                }

                // TODO: Implement JSON schema validation
                _logger.LogInformation("Schema validation not yet implemented");
            }

            // Report results
            if (validationErrors.Count > 0)
            {
                _logger.LogError("Configuration validation failed with {ErrorCount} errors:", validationErrors.Count);
                foreach (var error in validationErrors)
                {
                    _logger.LogError("  - {Error}", error);
                }
                return 1;
            }

            // Summary
            var deviceCount = (config.Devices?.Ios?.Count ?? 0) + (config.Devices?.Android?.Count ?? 0);
            var languageCount = config.Languages?.Count ?? 0;
            var screenshotCount = config.Screenshots?.Count ?? 0;
            
            _logger.LogInformation("✅ Configuration is valid");
            _logger.LogInformation("Summary: {DeviceCount} devices, {LanguageCount} languages, {ScreenshotCount} screenshots", 
                deviceCount, languageCount, screenshotCount);

            return 0;
        }
        catch (JsonException ex)
        {
            _logger.LogError("Invalid JSON format: {Error}", ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configuration validation failed");
            return 1;
        }
    }
}