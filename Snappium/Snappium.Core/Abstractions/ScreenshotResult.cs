namespace Snappium.Core.Abstractions;

/// <summary>
/// Result of a single screenshot capture operation.
/// </summary>
public sealed class ScreenshotResult
{
    /// <summary>
    /// Name of the screenshot (without extension).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full path to the saved screenshot file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Screenshot dimensions (width, height).
    /// </summary>
    public (int Width, int Height)? Dimensions { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long? FileSizeBytes { get; init; }

    /// <summary>
    /// Timestamp when the screenshot was captured.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Whether the screenshot was successfully captured.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if the screenshot failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}