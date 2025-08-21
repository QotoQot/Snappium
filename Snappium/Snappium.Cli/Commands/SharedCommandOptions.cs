using System.CommandLine;

namespace Snappium.Cli.Commands;

/// <summary>
/// Provides shared command-line options that are used across multiple commands.
/// This eliminates duplication and ensures consistency in option definitions.
/// </summary>
public static class SharedCommandOptions
{
    /// <summary>
    /// Creates the --config option for specifying configuration file path.
    /// </summary>
    public static Option<FileInfo> CreateConfigOption()
    {
        return new Option<FileInfo>(
            name: "--config",
            description: "Path to configuration JSON file")
        {
            IsRequired = true
        };
    }

    /// <summary>
    /// Creates the --platforms option for filtering platforms.
    /// </summary>
    public static Option<string[]> CreatePlatformsOption()
    {
        return new Option<string[]>(
            name: "--platforms", 
            description: "Comma-separated platforms (ios,android)")
        {
            AllowMultipleArgumentsPerToken = true
        };
    }

    /// <summary>
    /// Creates the --devices option for filtering devices.
    /// </summary>
    public static Option<string[]> CreateDevicesOption()
    {
        return new Option<string[]>(
            name: "--devices",
            description: "Comma-separated device names")
        {
            AllowMultipleArgumentsPerToken = true
        };
    }

    /// <summary>
    /// Creates the --langs option for filtering languages.
    /// </summary>
    public static Option<string[]> CreateLanguagesOption()
    {
        return new Option<string[]>(
            name: "--langs",
            description: "Comma-separated languages")
        {
            AllowMultipleArgumentsPerToken = true
        };
    }

    /// <summary>
    /// Creates the --screens option for filtering screenshots.
    /// </summary>
    public static Option<string[]> CreateScreenshotsOption()
    {
        return new Option<string[]>(
            name: "--screens",
            description: "Comma-separated screenshot names")
        {
            AllowMultipleArgumentsPerToken = true
        };
    }

    /// <summary>
    /// Creates the --output option for specifying output directory.
    /// </summary>
    public static Option<DirectoryInfo> CreateOutputOption()
    {
        return new Option<DirectoryInfo>(
            name: "--output",
            description: "Output directory for screenshots");
    }

    /// <summary>
    /// Adds all common filtering options to the specified command.
    /// This includes: --config, --platforms, --devices, --langs, --screens, --output
    /// </summary>
    /// <param name="command">The command to add options to</param>
    /// <returns>A tuple containing all the created options for easy access</returns>
    public static FilteringOptions AddFilteringOptions(Command command)
    {
        var configOption = CreateConfigOption();
        var platformsOption = CreatePlatformsOption();
        var devicesOption = CreateDevicesOption();
        var langsOption = CreateLanguagesOption();
        var screensOption = CreateScreenshotsOption();
        var outputOption = CreateOutputOption();

        command.AddOption(configOption);
        command.AddOption(platformsOption);
        command.AddOption(devicesOption);
        command.AddOption(langsOption);
        command.AddOption(screensOption);
        command.AddOption(outputOption);

        return new FilteringOptions(
            configOption,
            platformsOption,
            devicesOption,
            langsOption,
            screensOption,
            outputOption);
    }
}

/// <summary>
/// Contains references to all common filtering options for easy access in command handlers.
/// </summary>
/// <param name="Config">The --config option</param>
/// <param name="Platforms">The --platforms option</param>
/// <param name="Devices">The --devices option</param>
/// <param name="Languages">The --langs option</param>
/// <param name="Screenshots">The --screens option</param>
/// <param name="Output">The --output option</param>
public record FilteringOptions(
    Option<FileInfo> Config,
    Option<string[]> Platforms,
    Option<string[]> Devices,
    Option<string[]> Languages,
    Option<string[]> Screenshots,
    Option<DirectoryInfo> Output);