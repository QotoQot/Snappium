using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium.Appium;
using Snappium.Core.Abstractions;
using Snappium.Core.Appium;
using Snappium.Core.Appium;
using Snappium.Core.Config;
using Snappium.Core.DeviceManagement;
using Snappium.Core.Infrastructure;
using Snappium.Core.Logging;
using Snappium.Core.Planning;

namespace Snappium.Core.Orchestration;

/// <summary>
/// Main orchestrator for screenshot automation workflows.
/// </summary>
public sealed class Orchestrator : IOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAppiumServerController _appiumServerController;
    private readonly ILogger<Orchestrator> _logger;

    public Orchestrator(
        IServiceProvider serviceProvider,
        IAppiumServerController appiumServerController,
        ILogger<Orchestrator> logger)
    {
        _serviceProvider = serviceProvider;
        _appiumServerController = appiumServerController;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RunResult> ExecuteAsync(
        RunPlan runPlan,
        RootConfig config,
        CliOverrides? cliOverrides = null,
        CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTimeOffset.UtcNow;
        
        _logger.LogInformation("Starting screenshot run {RunId} with {JobCount} jobs", runId, runPlan.Jobs.Count);

        var jobResults = new List<JobResult>();
        var overallSuccess = true;
        string? runErrorMessage = null;

        try
        {
            // Execute jobs sequentially - one at a time to avoid device conflicts
            _logger.LogInformation("Executing {JobCount} jobs sequentially (one at a time)", 
                runPlan.Jobs.Count);

            foreach (var job in runPlan.Jobs)
            {
                try
                {
                    // Create a new JobExecutor instance for this job to ensure complete isolation
                    using var jobServiceProvider = CreateJobScopedServiceProvider();
                    var jobExecutor = jobServiceProvider.ServiceProvider.GetRequiredService<IJobExecutor>();
                    
                    var jobResult = await jobExecutor.ExecuteAsync(job, config, cliOverrides, cancellationToken);
                    jobResults.Add(jobResult);
                    
                    if (!jobResult.Success)
                    {
                        overallSuccess = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute job for {Platform} {Device}", job.Platform, job.DeviceFolder);
                    
                    jobResults.Add(new JobResult
                    {
                        Job = job,
                        Status = JobStatus.Failed,
                        StartTime = DateTimeOffset.UtcNow,
                        EndTime = DateTimeOffset.UtcNow,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        Screenshots = new List<ScreenshotResult>(),
                        FailureArtifacts = new List<FailureArtifact>()
                    });
                    
                    overallSuccess = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} failed with exception", runId);
            overallSuccess = false;
            runErrorMessage = ex.Message;
        }

        var endTime = DateTimeOffset.UtcNow;
        
        var runResult = new RunResult
        {
            RunId = runId,
            StartTime = startTime,
            EndTime = endTime,
            Success = overallSuccess,
            JobResults = jobResults,
            Environment = GetEnvironmentInfo(),
            ErrorMessage = runErrorMessage
        };

        _logger.LogInformation("Completed run {RunId} in {Duration}ms. Success: {Success}",
            runId, runResult.Duration.TotalMilliseconds, overallSuccess);

        return runResult;
    }

    /// <summary>
    /// Creates a scoped service provider for job execution to ensure complete isolation between parallel jobs.
    /// Each job gets its own dependency injection scope with separate instances of all services.
    /// </summary>
    private IServiceScope CreateJobScopedServiceProvider()
    {
        return _serviceProvider.CreateScope();
    }


    private static EnvironmentInfo GetEnvironmentInfo()
    {
        return new EnvironmentInfo
        {
            OperatingSystem = RuntimeInformation.OSDescription,
            DotNetVersion = RuntimeInformation.FrameworkDescription,
            Hostname = Environment.MachineName,
            WorkingDirectory = Environment.CurrentDirectory,
            SnappiumVersion = typeof(Orchestrator).Assembly.GetName().Version?.ToString() ?? "unknown"
        };
    }
}