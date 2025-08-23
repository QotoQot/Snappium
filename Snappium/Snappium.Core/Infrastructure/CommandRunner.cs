using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using Snappium.Core.Logging;
using System.Diagnostics;

namespace Snappium.Core.Infrastructure;

/// <summary>
/// Cross-platform command runner using CliWrap with timeout and retry support.
/// </summary>
public sealed class CommandRunner : ICommandRunner
{
    readonly ILogger<CommandRunner> _logger;
    readonly ISnappiumLogger? _snappiumLogger;
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);
    static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(1);

    public CommandRunner(ILogger<CommandRunner> logger, ISnappiumLogger? snappiumLogger = null)
    {
        _logger = logger;
        _snappiumLogger = snappiumLogger;
    }

    /// <inheritdoc />
    public async Task<CommandResult> RunAsync(
        string command,
        string[]? arguments = null,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var args = arguments ?? [];
        
        _logger.LogDebug("Executing command: {Command} {Arguments} (timeout: {Timeout})", 
            command, string.Join(" ", args), effectiveTimeout);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var cli = Cli.Wrap(command)
                .WithArguments(args)
                .WithValidation(CommandResultValidation.None); // We'll handle validation ourselves

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                cli = cli.WithWorkingDirectory(workingDirectory);
                _logger.LogDebug("Working directory: {WorkingDirectory}", workingDirectory);
            }

            using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            var result = await cli.ExecuteBufferedAsync(combinedCts.Token);
            
            stopwatch.Stop();

            var commandResult = new CommandResult
            {
                ExitCode = result.ExitCode,
                StandardOutput = result.StandardOutput,
                StandardError = result.StandardError,
                ExecutionTime = stopwatch.Elapsed
            };

            // Log with enhanced Snappium logger if available
            _snappiumLogger?.LogShellCommand(command, args, result.ExitCode, stopwatch.Elapsed, workingDirectory);

            _logger.LogDebug("Command completed with exit code {ExitCode} in {ExecutionTime}ms", 
                result.ExitCode, stopwatch.ElapsedMilliseconds);

            if (result.ExitCode != 0)
            {
                _logger.LogWarning("Command failed with exit code {ExitCode}. Stderr: {StandardError}", 
                    result.ExitCode, result.StandardError);
            }

            return commandResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning("Command cancelled after {ExecutionTime}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogError("Command timed out after {Timeout}", effectiveTimeout);
            throw new TimeoutException($"Command '{command}' timed out after {effectiveTimeout}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Command execution failed: {Command}", command);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CommandResult> RunWithRetryAsync(
        string command,
        string[]? arguments = null,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        int retryCount = 3,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveRetryDelay = retryDelay ?? DefaultRetryDelay;
        
        _logger.LogDebug("Executing command with retry: {Command} (retries: {RetryCount}, delay: {RetryDelay})", 
            command, retryCount, effectiveRetryDelay);

        for (int attempt = 0; attempt <= retryCount; attempt++)
        {
            try
            {
                var result = await RunAsync(command, arguments, workingDirectory, timeout, cancellationToken);
                
                // Consider non-zero exit codes as failures for retry logic
                if (!result.IsSuccess)
                {
                    if (attempt == retryCount)
                    {
                        throw new InvalidOperationException($"Command '{command}' failed with exit code {result.ExitCode}: {result.StandardError}");
                    }
                    
                    _logger.LogWarning("Command failed on attempt {Attempt}/{MaxAttempts}, retrying in {Delay}ms. Exit code: {ExitCode}",
                        attempt + 1, retryCount + 1, effectiveRetryDelay.TotalMilliseconds, result.ExitCode);
                    
                    await Task.Delay(effectiveRetryDelay, cancellationToken);
                    continue;
                }
                
                return result;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                if (attempt == retryCount)
                {
                    throw;
                }
                
                _logger.LogWarning("Command failed on attempt {Attempt}/{MaxAttempts}, retrying in {Delay}ms. Error: {Error}",
                    attempt + 1, retryCount + 1, effectiveRetryDelay.TotalMilliseconds, ex.Message);
                
                await Task.Delay(effectiveRetryDelay, cancellationToken);
            }
        }

        // This should never be reached
        throw new InvalidOperationException("Retry logic error");
    }
}