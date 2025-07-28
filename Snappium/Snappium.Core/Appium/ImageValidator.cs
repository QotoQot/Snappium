using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using Snappium.Core.Abstractions;
using Snappium.Core.Config;

namespace Snappium.Core.Appium;

/// <summary>
/// Validates screenshot dimensions using ImageSharp.
/// </summary>
public sealed class ImageValidator : IImageValidator
{
    private readonly ILogger<ImageValidator> _logger;

    public ImageValidator(ILogger<ImageValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ImageValidationResult> ValidateAsync(
        ScreenshotResult screenshotResult,
        string deviceFolder,
        Validation? validation,
        CancellationToken cancellationToken = default)
    {
        if (validation == null || validation.EnforceImageSize != true)
        {
            _logger.LogDebug("Image validation is disabled");
            return new ImageValidationResult
            {
                IsValid = true,
                ActualWidth = 0,
                ActualHeight = 0
            };
        }

        if (!File.Exists(screenshotResult.Path))
        {
            _logger.LogError("Screenshot file not found: {FilePath}", screenshotResult.Path);
            return new ImageValidationResult
            {
                IsValid = false,
                ActualWidth = 0,
                ActualHeight = 0,
                ErrorMessage = $"Screenshot file not found: {screenshotResult.Path}"
            };
        }

        try
        {
            // Load image and get dimensions
            using var image = await Image.LoadAsync(screenshotResult.Path, cancellationToken);
            var actualWidth = image.Width;
            var actualHeight = image.Height;

            _logger.LogDebug("Screenshot dimensions: {Width}x{Height} for {Name}",
                actualWidth, actualHeight, screenshotResult.Name);

            // Check if we have expected sizes for this device folder
            var platformSizes = validation.ExpectedSizes?.Ios ?? validation.ExpectedSizes?.Android;
            if (platformSizes == null || !platformSizes.TryGetValue(deviceFolder, out var deviceSize))
            {
                _logger.LogDebug("No expected size configured for device folder: {DeviceFolder}", deviceFolder);
                return new ImageValidationResult
                {
                    IsValid = true,
                    ActualWidth = actualWidth,
                    ActualHeight = actualHeight
                };
            }

            // Get expected dimensions based on orientation (assuming portrait for now)
            var expectedDimensions = deviceSize.Portrait;
            if (expectedDimensions == null || expectedDimensions.Length != 2)
            {
                _logger.LogDebug("No valid expected dimensions for device folder: {DeviceFolder}", deviceFolder);
                return new ImageValidationResult
                {
                    IsValid = true,
                    ActualWidth = actualWidth,
                    ActualHeight = actualHeight
                };
            }

            // Compare with expected dimensions
            var expectedWidth = expectedDimensions[0];
            var expectedHeight = expectedDimensions[1];
            var isValid = actualWidth == expectedWidth && actualHeight == expectedHeight;
            
            if (!isValid)
            {
                var errorMessage = $"Screenshot dimensions {actualWidth}x{actualHeight} do not match " +
                                   $"expected {expectedWidth}x{expectedHeight} for {deviceFolder}";

                if (validation.EnforceImageSize == true)
                {
                    _logger.LogError(errorMessage);
                }
                else
                {
                    _logger.LogWarning(errorMessage);
                }

                return new ImageValidationResult
                {
                    IsValid = validation.EnforceImageSize != true, // Only fail if configured to do so
                    ActualWidth = actualWidth,
                    ActualHeight = actualHeight,
                    ExpectedWidth = expectedWidth,
                    ExpectedHeight = expectedHeight,
                    ErrorMessage = errorMessage
                };
            }

            _logger.LogDebug("Screenshot dimensions validated successfully");
            return new ImageValidationResult
            {
                IsValid = true,
                ActualWidth = actualWidth,
                ActualHeight = actualHeight,
                ExpectedWidth = expectedWidth,
                ExpectedHeight = expectedHeight
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating screenshot: {FilePath}", screenshotResult.Path);
            return new ImageValidationResult
            {
                IsValid = false,
                ActualWidth = 0,
                ActualHeight = 0,
                ErrorMessage = $"Error loading image: {ex.Message}"
            };
        }
    }
}