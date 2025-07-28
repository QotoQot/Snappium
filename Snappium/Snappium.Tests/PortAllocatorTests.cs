using Snappium.Core.Planning;

namespace Snappium.Tests;

[TestFixture]
public class PortAllocatorTests
{
    [Test]
    public void AllocatePortsForJob_ValidJobIndex_ReturnsUniquePortAllocation()
    {
        // Arrange
        var allocator = new PortAllocator(basePort: 4723, portOffset: 10);

        // Act
        var allocation1 = allocator.AllocatePortsForJob(0);
        var allocation2 = allocator.AllocatePortsForJob(1);

        // Assert
        Assert.That(allocation1.AppiumPort, Is.EqualTo(4723));
        Assert.That(allocation1.WdaLocalPort, Is.EqualTo(4724));
        Assert.That(allocation1.SystemPort, Is.EqualTo(4725));

        Assert.That(allocation2.AppiumPort, Is.EqualTo(4733));
        Assert.That(allocation2.WdaLocalPort, Is.EqualTo(4734));
        Assert.That(allocation2.SystemPort, Is.EqualTo(4735));
    }

    [Test]
    public void AllocatePortsForJob_NegativeJobIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var allocator = new PortAllocator();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => allocator.AllocatePortsForJob(-1));
    }

    [Test]
    public void Constructor_InvalidBasePort_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortAllocator(basePort: 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortAllocator(basePort: 70000));
    }

    [Test]
    public void Constructor_InvalidPortOffset_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortAllocator(portOffset: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PortAllocator(portOffset: 200));
    }

    [Test]
    public void GetMaxParallelJobs_DefaultConfiguration_ReturnsReasonableLimit()
    {
        // Arrange
        var allocator = new PortAllocator(basePort: 4723, portOffset: 10);

        // Act
        var maxJobs = allocator.GetMaxParallelJobs();

        // Assert
        Assert.That(maxJobs, Is.GreaterThan(1000)); // Should support many parallel jobs
        Assert.That(maxJobs, Is.LessThan(10000)); // But not unlimited
    }

    [Test]
    public void ValidateAllocations_UniqueAllocations_ReturnsTrue()
    {
        // Arrange
        var allocator = new PortAllocator(basePort: 4723, portOffset: 10);
        var allocations = new[]
        {
            allocator.AllocatePortsForJob(0),
            allocator.AllocatePortsForJob(1),
            allocator.AllocatePortsForJob(2)
        };

        // Act
        var isValid = PortAllocator.ValidateAllocations(allocations);

        // Assert
        Assert.That(isValid, Is.True);
    }

    [Test]
    public void ValidateAllocations_ConflictingAllocations_ReturnsFalse()
    {
        // Arrange
        var allocator = new PortAllocator(basePort: 4723, portOffset: 10);
        var allocation1 = allocator.AllocatePortsForJob(0);
        var allocation2 = allocator.AllocatePortsForJob(0); // Same job index = same ports

        var allocations = new[] { allocation1, allocation2 };

        // Act
        var isValid = PortAllocator.ValidateAllocations(allocations);

        // Assert
        Assert.That(isValid, Is.False);
    }

    [Test]
    public void AllocatePortsForJob_ExceedsPortRange_ThrowsInvalidOperationException()
    {
        // Arrange
        var allocator = new PortAllocator(basePort: 65500, portOffset: 10);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => allocator.AllocatePortsForJob(10));
    }
}