using Snappium.Core.Abstractions;
using Snappium.Core.Config;

namespace Snappium.Core.Appium;

/// <summary>
/// Interface for validating screenshot dimensions.
/// </summary>
public interface IImageValidator
{
    /// <summary>
    /// Validate screenshot dimensions against expected sizes.
    /// </summary>
    /// <param name="screenshotResult">Screenshot result with file path</param>
    /// <param name="deviceFolder">Device folder name for looking up expected sizes</param>
    /// <param name="validation">Validation configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with any warnings or errors</returns>
    Task<ImageValidationResult> ValidateAsync(
        ScreenshotResult screenshotResult,
        string deviceFolder,
        Validation? validation,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of image dimension validation.
/// </summary>
public sealed record ImageValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Actual width of the image.
    /// </summary>
    public required int ActualWidth { get; init; }

    /// <summary>
    /// Actual height of the image.
    /// </summary>
    public required int ActualHeight { get; init; }

    /// <summary>
    /// Expected width of the image (if configured).
    /// </summary>
    public int? ExpectedWidth { get; init; }

    /// <summary>
    /// Expected height of the image (if configured).
    /// </summary>
    public int? ExpectedHeight { get; init; }

    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}