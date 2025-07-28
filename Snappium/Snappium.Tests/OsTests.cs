using Snappium.Core.Infrastructure;

namespace Snappium.Tests;

[TestFixture]
public class OsTests
{
    [Test]
    public void Name_ReturnsValidPlatformName()
    {
        // Act
        var name = Os.Name;

        // Assert
        Assert.That(name, Is.AnyOf("macOS", "Windows", "Linux", "Unknown"));
    }

    [Test]
    public void ExecutableExtension_ReturnsCorrectExtension()
    {
        // Act
        var extension = Os.ExecutableExtension;

        // Assert
        if (Os.IsWindows)
        {
            Assert.That(extension, Is.EqualTo(".exe"));
        }
        else
        {
            Assert.That(extension, Is.EqualTo(string.Empty));
        }
    }

    [Test]
    public void GetExecutableName_AppendsCorrectExtension()
    {
        // Arrange
        var baseName = "mycommand";

        // Act
        var executableName = Os.GetExecutableName(baseName);

        // Assert
        if (Os.IsWindows)
        {
            Assert.That(executableName, Is.EqualTo("mycommand.exe"));
        }
        else
        {
            Assert.That(executableName, Is.EqualTo("mycommand"));
        }
    }

    [Test]
    public void GetShellCommand_ReturnsCorrectShellForPlatform()
    {
        // Arrange
        var script = "echo hello";

        // Act
        var (command, arguments) = Os.GetShellCommand(script);

        // Assert
        if (Os.IsWindows)
        {
            Assert.That(command, Is.EqualTo("cmd"));
            Assert.That(arguments, Is.EqualTo(new[] { "/c", script }));
        }
        else
        {
            Assert.That(command, Is.EqualTo("sh"));
            Assert.That(arguments, Is.EqualTo(new[] { "-c", script }));
        }
    }

    [Test]
    public void GetCommonExecutablePaths_ReturnsNonEmptyArray()
    {
        // Act
        var paths = Os.GetCommonExecutablePaths();

        // Assert
        Assert.That(paths, Is.Not.Empty);
        Assert.That(paths.All(p => !string.IsNullOrEmpty(p)), Is.True);
    }

    [Test]
    public void PathEnvironmentVariable_ReturnsPath()
    {
        // Act
        var pathVar = Os.PathEnvironmentVariable;

        // Assert
        Assert.That(pathVar, Is.EqualTo("PATH"));
    }

    [Test]
    public void PathListSeparator_ReturnsCorrectSeparator()
    {
        // Act
        var separator = Os.PathListSeparator;

        // Assert
        if (Os.IsWindows)
        {
            Assert.That(separator, Is.EqualTo(';'));
        }
        else
        {
            Assert.That(separator, Is.EqualTo(':'));
        }
    }

    [Test]
    public void PathSeparator_ReturnsCorrectSeparator()
    {
        // Act
        var separator = Os.PathSeparator;

        // Assert
        Assert.That(separator, Is.EqualTo(Path.DirectorySeparatorChar));
    }

    [Test]
    public void PlatformDetection_OnlyOnePlatformIsTrue()
    {
        // Act & Assert
        var platformCount = new[] { Os.IsMacOS, Os.IsWindows, Os.IsLinux }.Count(p => p);
        Assert.That(platformCount, Is.EqualTo(1), "Exactly one platform should be detected as true");
    }
}