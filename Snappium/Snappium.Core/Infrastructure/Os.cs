using System.Runtime.InteropServices;

namespace Snappium.Core.Infrastructure;

/// <summary>
/// Operating system detection helper for cross-platform compatibility.
/// </summary>
public static class Os
{
    /// <summary>
    /// Gets whether the current platform is macOS.
    /// </summary>
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <summary>
    /// Gets whether the current platform is Windows.
    /// </summary>
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Gets whether the current platform is Linux.
    /// </summary>
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <summary>
    /// Gets a human-readable name for the current operating system.
    /// </summary>
    public static string Name
    {
        get
        {
            if (IsMacOS) return "macOS";
            if (IsWindows) return "Windows";
            if (IsLinux) return "Linux";
            return "Unknown";
        }
    }

    /// <summary>
    /// Gets the platform-specific executable extension.
    /// </summary>
    public static string ExecutableExtension => IsWindows ? ".exe" : string.Empty;

    /// <summary>
    /// Gets the platform-specific path separator.
    /// </summary>
    public static char PathSeparator => Path.DirectorySeparatorChar;

    /// <summary>
    /// Converts a command name to its platform-specific executable name.
    /// </summary>
    /// <param name="commandName">Base command name</param>
    /// <returns>Platform-specific executable name</returns>
    public static string GetExecutableName(string commandName)
    {
        return commandName + ExecutableExtension;
    }

    /// <summary>
    /// Gets the platform-specific shell command for executing scripts.
    /// </summary>
    /// <returns>Shell command and arguments</returns>
    public static (string Command, string[] Arguments) GetShellCommand(string script)
    {
        if (IsWindows)
        {
            return ("cmd", ["/c", script]);
        }
        else
        {
            return ("sh", ["-c", script]);
        }
    }

    /// <summary>
    /// Gets common executable search paths for the current platform.
    /// </summary>
    /// <returns>Array of common executable paths</returns>
    public static string[] GetCommonExecutablePaths()
    {
        if (IsWindows)
        {
            return
            [
                @"C:\Program Files\",
                @"C:\Program Files (x86)\",
                @"C:\Windows\System32\",
                @"C:\Windows\"
            ];
        }
        else
        {
            return
            [
                "/usr/local/bin/",
                "/usr/bin/",
                "/bin/",
                "/opt/homebrew/bin/", // Apple Silicon Homebrew
                "/opt/local/bin/"     // MacPorts
            ];
        }
    }

    /// <summary>
    /// Gets environment variable names that contain executable search paths.
    /// </summary>
    /// <returns>Environment variable name for executable paths</returns>
    public static string PathEnvironmentVariable => IsWindows ? "PATH" : "PATH";

    /// <summary>
    /// Gets the character used to separate paths in the PATH environment variable.
    /// </summary>
    /// <returns>Path separator character</returns>
    public static char PathListSeparator => IsWindows ? ';' : ':';
}