namespace Snappium.Core.Orchestration;

/// <summary>
/// Writer for run manifests and summaries.
/// </summary>
public interface IManifestWriter
{
    /// <summary>
    /// Write run manifest JSON and summary text files.
    /// </summary>
    /// <param name="runResult">Run result to write</param>
    /// <param name="outputDirectory">Output directory for manifest files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paths to the written files</returns>
    Task<ManifestFiles> WriteAsync(
        RunResult runResult,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Paths to manifest files that were written.
/// </summary>
public sealed record ManifestFiles
{
    /// <summary>
    /// Path to the run_manifest.json file.
    /// </summary>
    public required string ManifestJsonPath { get; init; }

    /// <summary>
    /// Path to the run_summary.txt file.
    /// </summary>
    public required string SummaryTextPath { get; init; }
}