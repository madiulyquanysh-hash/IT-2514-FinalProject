using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class DeferredExecutionTests
{
    private static readonly string[] Lines =
    {
        Support.BookingsHeader,
        "B-1,Alice,Tech Conference,2,WELCOME10,2026-10-01",
        "B-2,Bob,Jazz Night,1,,2026-10-02",
        "B-3,Carol,Jazz Night,2,,2026-10-03"
    };

    private static IEnumerable<string> Counting(Action onPull)
    {
        foreach (var line in Lines)
        {
            onPull();
            yield return line;
        }
    }

    private static Result<ProcessedBooking, AppError> Accept(BookingEntry entry) =>
        Result<ProcessedBooking, AppError>.Ok(Support.Processed(entry.Reference, "Event", 1, 10m, 1, "2026-10-01"));

    private static BookingEntry[] Entries() => new[]
    {
        Support.Entry("B-1", "Event", 1),
        Support.Entry("B-2", "Event", 1),
        Support.Entry("B-3", "Event", 1)
    };

    [Fact]
    public void ParseEntries_BeforeEnumeration_PullsNothing()
    {
        var pulled = 0;

        var stream = BookingStream.ParseEntries("memory", Counting(() => pulled++));

        Assert.NotNull(stream);
        Assert.Equal(0, pulled);
    }

    [Fact]
    public void ParseEntries_DuringIteration_PullsOnlyWhatIsNeeded()
    {
        var pulled = 0;
        var stream = BookingStream.ParseEntries("memory", Counting(() => pulled++));

        var first = Support.ExpectSuccess(stream.First());

        Assert.Equal("B-1", first.Reference);
        Assert.Equal(2, pulled);
    }

    [Fact]
    public void ParseEntries_Materialised_ReuseDoesNotPullAgain()
    {
        var pulled = 0;
        var stream = BookingStream.ParseEntries("memory", Counting(() => pulled++));

        var snapshot = stream.ToArray();
        var afterMaterialising = pulled;
        _ = snapshot.Count();
        _ = snapshot.Select(item => item).ToArray();

        Assert.Equal(3, snapshot.Length);
        Assert.Equal(Lines.Length, afterMaterialising);
        Assert.Equal(afterMaterialising, pulled);
    }

    [Fact]
    public void ParseEntries_RepeatedLazyEnumeration_PullsAgain()
    {
        var pulled = 0;
        var stream = BookingStream.ParseEntries("memory", Counting(() => pulled++));

        _ = stream.ToArray();
        _ = stream.ToArray();

        Assert.Equal(Lines.Length * 2, pulled);
    }

    [Fact]
    public void Stream_BeforeEnumeration_ProcessesNothing()
    {
        var calls = 0;

        var stream = BookingBatchProcessor.Stream(Entries(), entry =>
        {
            calls++;
            return Accept(entry);
        });

        Assert.NotNull(stream);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Stream_DuringIteration_ProcessesOnDemand()
    {
        var calls = 0;
        var stream = BookingBatchProcessor.Stream(Entries(), entry =>
        {
            calls++;
            return Accept(entry);
        });

        _ = stream.First();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Stream_Snapshot_ReuseDoesNotRepeatProcessing()
    {
        var calls = 0;
        var stream = BookingBatchProcessor.Stream(Entries(), entry =>
        {
            calls++;
            return Accept(entry);
        });

        var snapshot = stream.ToArray();
        _ = snapshot.Length;
        _ = snapshot.Count();
        _ = snapshot.ToList();

        Assert.Equal(3, snapshot.Length);
        Assert.Equal(3, calls);
    }

    [Fact]
    public void Stream_RepeatedLazyEnumeration_RepeatsProcessing()
    {
        var calls = 0;
        var stream = BookingBatchProcessor.Stream(Entries(), entry =>
        {
            calls++;
            return Accept(entry);
        });

        _ = stream.ToArray();
        _ = stream.ToArray();

        Assert.Equal(6, calls);
    }
}