using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class SeatAllocatorTests
{
    private static SeatInventory Inventory() => SeatInventory.FromCatalog(Samples.Events());

    private static ProcessedBooking Jazz(string reference, int quantity, string date) =>
        Support.Processed(reference, "Jazz Night", quantity, quantity * 45.5m, 100, date);

    [Fact]
    public void Allocate_FirstComeFirstServed_WaitlistsWhatDoesNotFit()
    {
        var bookings = new[] { Jazz("B-2", 4, "2026-10-02"), Jazz("B-4", 6, "2026-10-07"), Jazz("B-11", 2, "2026-10-16") };

        var result = SeatAllocator.Allocate(bookings, Inventory());

        Assert.Equal(new[] { "B-2", "B-4" }, result.Allocated.Select(b => b.Entry.Reference));
        Assert.Equal(new[] { "B-11" }, result.Waitlisted.Select(b => b.Entry.Reference));
        Assert.Equal(ErrorCode.CapacityExceeded, result.Waitlisted[0].Error.Code);
        Assert.Equal(0, Assert.IsType<Maybe<SeatLevel>.Some>(result.Inventory.Find("Jazz Night")).Value.Remaining);
    }

    [Fact]
    public void Allocate_InputOrderDoesNotMatter()
    {
        var bookings = new[] { Jazz("B-11", 2, "2026-10-16"), Jazz("B-4", 6, "2026-10-07"), Jazz("B-2", 4, "2026-10-02") };

        var result = SeatAllocator.Allocate(bookings, Inventory());

        Assert.Equal(new[] { "B-2", "B-4" }, result.Allocated.Select(b => b.Entry.Reference));
        Assert.Equal(new[] { "B-11" }, result.Waitlisted.Select(b => b.Entry.Reference));
    }

    [Fact]
    public void Allocate_SameDate_TieBreaksByReference()
    {
        var bookings = new[] { Jazz("B-9", 6, "2026-10-02"), Jazz("B-1", 6, "2026-10-02") };

        var result = SeatAllocator.Allocate(bookings, Inventory());

        Assert.Equal(new[] { "B-1" }, result.Allocated.Select(b => b.Entry.Reference));
        Assert.Equal(new[] { "B-9" }, result.Waitlisted.Select(b => b.Entry.Reference));
    }

    [Fact]
    public void Allocate_TracksEachEventSeparately()
    {
        var bookings = new[]
        {
            Support.Processed("T-1", "Tech Conference", 3, 450m, 100, "2026-10-01"),
            Jazz("J-1", 11, "2026-10-01")
        };

        var result = SeatAllocator.Allocate(bookings, Inventory());

        Assert.Equal(new[] { "T-1" }, result.Allocated.Select(b => b.Entry.Reference));
        Assert.Equal(new[] { "J-1" }, result.Waitlisted.Select(b => b.Entry.Reference));
        Assert.Equal(47, Assert.IsType<Maybe<SeatLevel>.Some>(result.Inventory.Find("Tech Conference")).Value.Remaining);
    }

    [Fact]
    public void Allocate_EmptyInput_ReturnsUnchangedInventory()
    {
        var inventory = Inventory();

        var result = SeatAllocator.Allocate(Array.Empty<ProcessedBooking>(), inventory);

        Assert.Empty(result.Allocated);
        Assert.Empty(result.Waitlisted);
        Assert.Equal(inventory.Levels, result.Inventory.Levels);
    }

    [Fact]
    public void Allocate_DoesNotModifyInputInventory()
    {
        var inventory = Inventory();

        _ = SeatAllocator.Allocate(new[] { Jazz("B-2", 4, "2026-10-02") }, inventory);

        Assert.Equal(10, Assert.IsType<Maybe<SeatLevel>.Some>(inventory.Find("Jazz Night")).Value.Remaining);
    }
}
