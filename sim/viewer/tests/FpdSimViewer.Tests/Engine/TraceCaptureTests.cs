using FluentAssertions;
using FpdSimViewer.Engine;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Engine;

public sealed class TraceCaptureTests
{
    [Fact]
    public void Record_ShouldStoreSnapshots()
    {
        var trace = new TraceCapture();

        for (ulong cycle = 0; cycle < 10; cycle++)
        {
            trace.Record(CreateSnapshot(cycle));
        }

        trace.Count.Should().Be(10);
        trace.GetAt(0).Should().NotBeNull();
        trace.GetAt(9)!.Cycle.Should().Be(9UL);
    }

    [Fact]
    public void CircularBuffer_ShouldOverwriteOldest()
    {
        var trace = new TraceCapture(capacity: 4);

        for (ulong cycle = 0; cycle < 6; cycle++)
        {
            trace.Record(CreateSnapshot(cycle));
        }

        trace.Count.Should().Be(4);
        trace.GetAt(0)!.Cycle.Should().Be(2UL);
        trace.GetAt(3)!.Cycle.Should().Be(5UL);
    }

    [Fact]
    public void Clear_ShouldResetBuffer()
    {
        var trace = new TraceCapture();
        for (ulong cycle = 0; cycle < 5; cycle++)
        {
            trace.Record(CreateSnapshot(cycle));
        }

        trace.Clear();

        trace.Count.Should().Be(0);
        trace.GetAt(0).Should().BeNull();
    }

    private static SimulationSnapshot CreateSnapshot(ulong cycle)
    {
        return new SimulationSnapshot(
            cycle,
            0U,
            "IDLE",
            false,
            false,
            false,
            0U,
            0U,
            1U,
            false,
            false,
            false,
            false,
            new SignalMap(),
            false,
            false,
            0U,
            false,
            false,
            false,
            false,
            FoundationConstants.MakeDefaultRegisters());
    }
}
