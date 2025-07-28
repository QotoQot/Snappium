using Microsoft.Extensions.Logging;
using Snappium.Core.Config;
using System.Text.Json;

namespace Snappium.Tests;

[TestFixture]
public class ConfigLoaderTests
{
    private ConfigLoader _configLoader = null!;
    private ILogger<ConfigLoader> _logger = null!;
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ConfigLoader>();
        _configLoader = new ConfigLoader(_logger);
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Test]
    public async Task LoadAsync_ValidConfig_ReturnsConfiguration()
    {
        // Arrange
        var configPath = Path.Combine(_tempDir, "config.json");
        var validConfig = CreateValidConfigJson();
        await File.WriteAllTextAsync(configPath, validConfig);

        // Act
        var result = await _configLoader.LoadAsync(configPath);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Languages, Has.Count.EqualTo(2));
        Assert.That(result.Languages, Contains.Item("en-US"));
        Assert.That(result.Languages, Contains.Item("es-ES"));
        Assert.That(result.Devices.Ios, Has.Count.EqualTo(1));
        Assert.That(result.Devices.Android, Has.Count.EqualTo(1));
        Assert.That(result.Screenshots, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadAsync_MissingLocaleMapping_ThrowsException()
    {
        // Arrange
        var configPath = Path.Combine(_tempDir, "config.json");
        var invalidConfig = CreateConfigWithMissingLocaleMapping();
        await File.WriteAllTextAsync(configPath, invalidConfig);

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _configLoader.LoadAsync(configPath));
        Assert.That(ex.Message, Does.Contain("Languages missing from locale_mapping"));
        Assert.That(ex.Message, Does.Contain("fr-FR"));
    }

    [Test]
    public async Task LoadAsync_DuplicateFolders_ThrowsException()
    {
        // Arrange
        var configPath = Path.Combine(_tempDir, "config.json");
        var invalidConfig = CreateConfigWithDuplicateFolders();
        await File.WriteAllTextAsync(configPath, invalidConfig);

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _configLoader.LoadAsync(configPath));
        Assert.That(ex.Message, Does.Contain("Device folders must be unique"));
        Assert.That(ex.Message, Does.Contain("Phone_6_7"));
    }

    [Test]
    public async Task LoadAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDir, "nonexistent.json");

        // Act & Assert
        var ex = Assert.ThrowsAsync<FileNotFoundException>(
            () => _configLoader.LoadAsync(nonExistentPath));
        Assert.That(ex.Message, Does.Contain("Configuration file"));
        Assert.That(ex.Message, Does.Contain("not found"));
    }

    [Test]
    public async Task LoadAsync_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        var configPath = Path.Combine(_tempDir, "config.json");
        await File.WriteAllTextAsync(configPath, "{ invalid json }");

        // Act & Assert
        Assert.ThrowsAsync<JsonException>(() => _configLoader.LoadAsync(configPath));
    }

    private static string CreateValidConfigJson()
    {
        return """
        {
          "Devices": {
            "Ios": [
              {
                "Name": "iPhone 15 Pro Max",
                "Udid": null,
                "Folder": "iPhone_15_ProMax_6_7",
                "PlatformVersion": "17.5"
              }
            ],
            "Android": [
              {
                "Name": "Pixel 7 Pro",
                "Avd": "Pixel_7_Pro_API_34",
                "Folder": "Phone_6_7",
                "PlatformVersion": "14"
              }
            ]
          },
          "Languages": ["en-US", "es-ES"],
          "LocaleMapping": {
            "en-US": {"Ios": "en_US", "Android": "en_US"},
            "es-ES": {"Ios": "es_ES", "Android": "es_ES"}
          },
          "Screenshots": [
            {
              "Name": "01_home",
              "Orientation": "portrait",
              "Actions": [
                {"wait": {"seconds": 2}},
                {"capture": {"name": "01_home"}}
              ],
              "Assert": {
                "ios": {"AccessibilityId": "tab-bar"},
                "android": {"AccessibilityId": "bottom_navigation"}
              }
            }
          ]
        }
        """;
    }

    private static string CreateConfigWithMissingLocaleMapping()
    {
        return """
        {
          "Devices": {
            "Ios": [
              {
                "Name": "iPhone 15 Pro Max",
                "Udid": null,
                "Folder": "iPhone_15_ProMax_6_7",
                "PlatformVersion": "17.5"
              }
            ],
            "Android": [
              {
                "Name": "Pixel 7 Pro",
                "Avd": "Pixel_7_Pro_API_34",
                "Folder": "Phone_6_7",
                "PlatformVersion": "14"
              }
            ]
          },
          "Languages": ["en-US", "fr-FR"],
          "LocaleMapping": {
            "en-US": {"Ios": "en_US", "Android": "en_US"}
          },
          "Screenshots": [
            {
              "Name": "01_home",
              "Actions": [
                {"capture": {"name": "01_home"}}
              ]
            }
          ]
        }
        """;
    }

    private static string CreateConfigWithDuplicateFolders()
    {
        return """
        {
          "Devices": {
            "Ios": [
              {
                "Name": "iPhone 15 Pro Max",
                "Udid": null,
                "Folder": "Phone_6_7",
                "PlatformVersion": "17.5"
              }
            ],
            "Android": [
              {
                "Name": "Pixel 7 Pro",
                "Avd": "Pixel_7_Pro_API_34",
                "Folder": "Phone_6_7",
                "PlatformVersion": "14"
              }
            ]
          },
          "Languages": ["en-US"],
          "LocaleMapping": {
            "en-US": {"Ios": "en_US", "Android": "en_US"}
          },
          "Screenshots": [
            {
              "Name": "01_home",
              "Actions": [
                {"capture": {"name": "01_home"}}
              ]
            }
          ]
        }
        """;
    }
}