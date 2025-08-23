using Snappium.Core.Abstractions;
using Snappium.Core.Config;

namespace Snappium.Core.Planning;

/// <summary>
/// Allocates unique ports for parallel job execution to avoid conflicts.
/// </summary>
public sealed class PortAllocator
{
    readonly int _basePort;
    readonly int _portOffset;

    /// <summary>
    /// Initialize a new port allocator.
    /// </summary>
    /// <param name="basePort">Base port to start allocation from</param>
    /// <param name="portOffset">Offset between each job's port range</param>
    public PortAllocator(int basePort = Defaults.Ports.AppiumBasePort, int portOffset = Defaults.Ports.PortOffset)
    {
        if (basePort < Defaults.Ports.MinPortNumber || basePort > Defaults.Ports.MaxPortNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(basePort), $"Base port must be between {Defaults.Ports.MinPortNumber} and {Defaults.Ports.MaxPortNumber}");
        }

        if (portOffset < 1 || portOffset > Defaults.Ports.MaxPortOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(portOffset), $"Port offset must be between 1 and {Defaults.Ports.MaxPortOffset}");
        }

        _basePort = basePort;
        _portOffset = portOffset;
    }

    /// <summary>
    /// Allocate a unique set of ports for a job.
    /// </summary>
    /// <param name="jobIndex">Zero-based job index</param>
    /// <returns>Port allocation for the job</returns>
    public PortAllocation AllocatePortsForJob(int jobIndex)
    {
        if (jobIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(jobIndex), "Job index must be non-negative");
        }

        var jobPortBase = _basePort + (jobIndex * _portOffset);

        // Ensure we don't exceed port range
        if (jobPortBase + _portOffset > Defaults.Ports.MaxPortNumber)
        {
            throw new InvalidOperationException($"Port allocation would exceed maximum port number. Job index {jobIndex} with base {_basePort} and offset {_portOffset}");
        }

        return new PortAllocation(
            AppiumPort: jobPortBase,
            WdaLocalPort: jobPortBase + 1,
            SystemPort: jobPortBase + 2
        );
    }

    /// <summary>
    /// Calculate the maximum number of parallel jobs that can be supported with the current configuration.
    /// </summary>
    /// <returns>Maximum number of parallel jobs</returns>
    public int GetMaxParallelJobs()
    {
        var maxPortsAvailable = 65535 - _basePort;
        return maxPortsAvailable / _portOffset;
    }

    /// <summary>
    /// Validate that a set of port allocations don't conflict with each other.
    /// </summary>
    /// <param name="allocations">Port allocations to validate</param>
    /// <returns>True if all allocations are unique</returns>
    public static bool ValidateAllocations(IEnumerable<PortAllocation> allocations)
    {
        var usedPorts = new HashSet<int>();

        foreach (var allocation in allocations)
        {
            var ports = new[] { allocation.AppiumPort, allocation.WdaLocalPort, allocation.SystemPort };

            foreach (var port in ports)
            {
                if (!usedPorts.Add(port))
                {
                    return false; // Duplicate port found
                }
            }
        }

        return true;
    }
}