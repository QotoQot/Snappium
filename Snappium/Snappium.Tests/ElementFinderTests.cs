using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using Snappium.Core.Appium;
using Snappium.Core.Config;

namespace Snappium.Tests;

[TestFixture]
public class ElementFinderTests
{
    private ElementFinder _elementFinder = null!;
    private ILogger<ElementFinder> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<ElementFinder>();
        _elementFinder = new ElementFinder(_logger);
    }

    [Test]
    public void GetByFromSelector_AccessibilityId_ReturnsMobileBy()
    {
        // Arrange
        var selector = new Selector { AccessibilityId = "test-id" };

        // Act
        var by = _elementFinder.GetByFromSelector(selector);

        // Assert
        Assert.That(by, Is.Not.Null);
        Assert.That(by.ToString(), Does.Contain("test-id"));
    }

    [Test]
    public void GetByFromSelector_Id_ReturnsById()
    {
        // Arrange
        var selector = new Selector { Id = "resource-id" };

        // Act
        var by = _elementFinder.GetByFromSelector(selector);

        // Assert
        Assert.That(by, Is.Not.Null);
        Assert.That(by.ToString(), Does.Contain("resource-id"));
    }

    [Test]
    public void GetByFromSelector_IosClassChain_ReturnsMobileBy()
    {
        // Arrange
        var selector = new Selector { IosClassChain = "**/XCUIElementTypeButton[`name == 'Test'`]" };

        // Act
        var by = _elementFinder.GetByFromSelector(selector);

        // Assert
        Assert.That(by, Is.Not.Null);
        Assert.That(by.ToString(), Does.Contain("XCUIElementTypeButton"));
    }

    [Test]
    public void GetByFromSelector_AndroidUiautomator_ReturnsMobileBy()
    {
        // Arrange
        var selector = new Selector { AndroidUiautomator = "new UiSelector().text(\"Test\")" };

        // Act
        var by = _elementFinder.GetByFromSelector(selector);

        // Assert
        Assert.That(by, Is.Not.Null);
        Assert.That(by.ToString(), Does.Contain("UiSelector"));
    }

    [Test]
    public void GetByFromSelector_Xpath_ReturnsByXPath()
    {
        // Arrange
        var selector = new Selector { Xpath = "//button[@id='test']" };

        // Act
        var by = _elementFinder.GetByFromSelector(selector);

        // Assert
        Assert.That(by, Is.Not.Null);
        Assert.That(by.ToString(), Does.Contain("button"));
    }

    [Test]
    public void GetByFromSelector_NoSelectors_ThrowsArgumentException()
    {
        // Arrange
        var selector = new Selector();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _elementFinder.GetByFromSelector(selector));
    }

    [Test]
    public void GetByFromSelector_MultipleSelectors_UsesFirstAvailable()
    {
        // Arrange
        var selector = new Selector 
        { 
            AccessibilityId = "acc-id",
            Id = "resource-id",
            Xpath = "//button"
        };

        // Act
        var by = _elementFinder.GetByFromSelector(selector);

        // Assert
        Assert.That(by, Is.Not.Null);
        // Should use AccessibilityId as it's checked first
        Assert.That(by.ToString(), Does.Contain("acc-id"));
    }
}