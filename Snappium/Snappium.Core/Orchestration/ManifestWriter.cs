using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Snappium.Core.Orchestration;

/// <summary>
/// Writer for run manifests and summaries.
/// </summary>
public sealed class ManifestWriter : IManifestWriter
{
    private readonly ILogger<ManifestWriter> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public ManifestWriter(ILogger<ManifestWriter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ManifestFiles> WriteAsync(
        RunResult runResult,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var manifestJsonPath = Path.Combine(outputDirectory, "run_manifest.json");
        var summaryTextPath = Path.Combine(outputDirectory, "run_summary.txt");

        // Write JSON manifest
        var manifest = CreateManifest(runResult);
        var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
        await File.WriteAllTextAsync(manifestJsonPath, manifestJson, cancellationToken);

        _logger.LogInformation("Written run manifest: {Path}", manifestJsonPath);

        // Write human-readable summary
        var summary = CreateSummary(runResult);
        await File.WriteAllTextAsync(summaryTextPath, summary, cancellationToken);

        _logger.LogInformation("Written run summary: {Path}", summaryTextPath);

        return new ManifestFiles
        {
            ManifestJsonPath = manifestJsonPath,
            SummaryTextPath = summaryTextPath
        };
    }

    private static object CreateManifest(RunResult runResult)
    {
        return new
        {
            run_id = runResult.RunId,
            start_time = runResult.StartTime,
            end_time = runResult.EndTime,
            duration_ms = (int)runResult.Duration.TotalMilliseconds,
            success = runResult.Success,
            environment = new
            {
                operating_system = runResult.Environment.OperatingSystem,
                dotnet_version = runResult.Environment.DotNetVersion,
                hostname = runResult.Environment.Hostname,
                working_directory = runResult.Environment.WorkingDirectory,
                snappium_version = runResult.Environment.SnappiumVersion
            },
            summary = new
            {
                total_jobs = runResult.JobResults.Count,
                successful_jobs = runResult.JobResults.Count(j => j.Success),
                failed_jobs = runResult.JobResults.Count(j => !j.Success),
                total_screenshots = runResult.JobResults.Sum(j => j.Screenshots.Count),
                total_failure_artifacts = runResult.JobResults.Sum(j => j.FailureArtifacts.Count)
            },
            jobs = runResult.JobResults.Select(job => new
            {
                job_id = $"{job.Job.Platform}_{job.Job.DeviceFolder}_{job.Job.Language}",
                platform = job.Job.Platform.ToString().ToLowerInvariant(),
                device_folder = job.Job.DeviceFolder,
                language = job.Job.Language,
                start_time = job.StartTime,
                end_time = job.EndTime,
                duration_ms = job.Duration?.TotalMilliseconds,
                status = job.Status.ToString().ToLowerInvariant(),
                success = job.Success,
                error_message = job.ErrorMessage,
                screenshots = job.Screenshots.Select(screenshot => new
                {
                    name = screenshot.Name,
                    path = screenshot.Path,
                    timestamp = screenshot.Timestamp,
                    success = screenshot.Success,
                    dimensions = screenshot.Dimensions != null ? new
                    {
                        width = screenshot.Dimensions.Value.Width,
                        height = screenshot.Dimensions.Value.Height
                    } : null,
                    file_size_bytes = screenshot.FileSizeBytes,
                    error_message = screenshot.ErrorMessage
                }).ToList(),
                failure_artifacts = job.FailureArtifacts.Select(artifact => new
                {
                    type = artifact.Type.ToString().ToLowerInvariant(),
                    path = artifact.Path,
                    timestamp = artifact.Timestamp,
                    file_size_bytes = artifact.FileSizeBytes
                }).ToList()
            }).ToList(),
            error_message = runResult.ErrorMessage
        };
    }

    private static string CreateSummary(RunResult runResult)
    {
        var summary = new StringBuilder();
        
        summary.AppendLine("Snappium Screenshot Run Summary");
        summary.AppendLine("================================");
        summary.AppendLine();
        
        summary.AppendLine($"Run ID: {runResult.RunId}");
        summary.AppendLine($"Start Time: {runResult.StartTime:yyyy-MM-dd HH:mm:ss UTC}");
        summary.AppendLine($"End Time: {runResult.EndTime:yyyy-MM-dd HH:mm:ss UTC}");
        summary.AppendLine($"Duration: {runResult.Duration.TotalSeconds:F1} seconds");
        summary.AppendLine($"Overall Success: {(runResult.Success ? "✅ Yes" : "❌ No")}");
        
        if (!string.IsNullOrEmpty(runResult.ErrorMessage))
        {
            summary.AppendLine($"Run Error: {runResult.ErrorMessage}");
        }
        
        summary.AppendLine();

        // Environment
        summary.AppendLine("Environment:");
        summary.AppendLine($"  OS: {runResult.Environment.OperatingSystem}");
        summary.AppendLine($"  .NET: {runResult.Environment.DotNetVersion}");
        summary.AppendLine($"  Host: {runResult.Environment.Hostname}");
        summary.AppendLine($"  Snappium: {runResult.Environment.SnappiumVersion}");
        summary.AppendLine();

        // Summary stats
        var totalJobs = runResult.JobResults.Count;
        var successfulJobs = runResult.JobResults.Count(j => j.Success);
        var failedJobs = runResult.JobResults.Count(j => !j.Success);
        var totalScreenshots = runResult.JobResults.Sum(j => j.Screenshots.Count);
        var totalFailureArtifacts = runResult.JobResults.Sum(j => j.FailureArtifacts.Count);

        summary.AppendLine("Summary:");
        summary.AppendLine($"  Total Jobs: {totalJobs}");
        summary.AppendLine($"  Successful: {successfulJobs}");
        summary.AppendLine($"  Failed: {failedJobs}");
        summary.AppendLine($"  Screenshots: {totalScreenshots}");
        summary.AppendLine($"  Failure Artifacts: {totalFailureArtifacts}");
        summary.AppendLine();

        // Job details
        summary.AppendLine("Job Results:");
        foreach (var jobResult in runResult.JobResults)
        {
            var status = jobResult.Success ? "✅" : "❌";
            var duration = jobResult.Duration?.TotalSeconds.ToString("F1") ?? "?";
            
            summary.AppendLine($"  {status} {jobResult.Job.Platform} {jobResult.Job.DeviceFolder} {jobResult.Job.Language} ({duration}s)");
            
            if (!string.IsNullOrEmpty(jobResult.ErrorMessage))
            {
                summary.AppendLine($"    Error: {jobResult.ErrorMessage}");
            }
            
            if (jobResult.Screenshots.Count > 0)
            {
                summary.AppendLine($"    Screenshots: {jobResult.Screenshots.Count}");
                foreach (var screenshot in jobResult.Screenshots.Take(3))
                {
                    var screenshotStatus = screenshot.Success ? "✅" : "❌";
                    summary.AppendLine($"      {screenshotStatus} {screenshot.Name}");
                }
                if (jobResult.Screenshots.Count > 3)
                {
                    summary.AppendLine($"      ... and {jobResult.Screenshots.Count - 3} more");
                }
            }
            
            if (jobResult.FailureArtifacts.Count > 0)
            {
                summary.AppendLine($"    Failure Artifacts: {jobResult.FailureArtifacts.Count}");
                foreach (var artifact in jobResult.FailureArtifacts)
                {
                    summary.AppendLine($"      📄 {artifact.Type}: {Path.GetFileName(artifact.Path)}");
                }
            }
            
            summary.AppendLine();
        }

        if (failedJobs > 0)
        {
            summary.AppendLine($"⚠️  {failedJobs} job(s) failed. Check the detailed logs and failure artifacts above.");
        }
        else if (totalJobs > 0)
        {
            summary.AppendLine("🎉 All jobs completed successfully!");
        }

        return summary.ToString();
    }
}