using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class SeatInventoryTests
{
    private static SeatInventory Inventory() => SeatInventory.FromCatalog(Samples.Events());

    private static int RemainingOf(SeatInventory inventory, string eventName) =>
        Assert.IsType<Maybe<SeatLevel>.Some>(inventory.Find(eventName)).Value.Remaining;

    [Fact]
    public void FromCatalog_ListsFullLevelsOrderedByName()
    {
        var levels = Inventory().Levels;

        Assert.Equal(new[] { "Jazz Night", "Sold Out Gala", "Tech Conference" }, levels.Select(l => l.EventName));
        Assert.All(levels, level => Assert.Equal(level.Capacity, level.Remaining));
    }

    [Fact]
    public void Find_ZeroCapacityEvent_IsPresent()
    {
        var level = Assert.IsType<Maybe<SeatLevel>.Some>(Inventory().Find("Sold Out Gala")).Value;

        Assert.Equal(0, level.Remaining);
    }

    [Fact]
    public void Find_UnknownEvent_ReturnsNone()
    {
        Assert.IsType<Maybe<SeatLevel>.None>(Inventory().Find("Unknown Event"));
    }

    [Fact]
    public void Reserve_WithinCapacity_ReturnsNewInventoryAndKeepsOriginal()
    {
        var original = Inventory();

        var reserved = Support.ExpectSuccess(original.Reserve("Jazz Night", 4));

        Assert.Equal(6, RemainingOf(reserved, "Jazz Night"));
        Assert.Equal(10, RemainingOf(original, "Jazz Night"));
    }

    [Fact]
    public void Reserve_ExactRemaining_Succeeds()
    {
        var reserved = Support.ExpectSuccess(Inventory().Reserve("Jazz Night", 10));

        Assert.Equal(0, RemainingOf(reserved, "Jazz Night"));
    }

    [Fact]
    public void Reserve_ExceedsRemaining_ReturnsCapacityExceeded()
    {
        var error = Support.ExpectFailure(Inventory().Reserve("Jazz Night", 11));

        Assert.Equal(ErrorCode.CapacityExceeded, error.Code);
        Assert.Equal("Jazz Night", error.Subject);
    }

    [Fact]
    public void Reserve_UnknownEvent_ReturnsEventNotFound()
    {
        var error = Support.ExpectFailure(Inventory().Reserve("Unknown Event", 1));

        Assert.Equal(ErrorCode.EventNotFound, error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Reserve_NonPositiveQuantity_ReturnsInvalidQuantity(int quantity)
    {
        var error = Support.ExpectFailure(Inventory().Reserve("Jazz Night", quantity));

        Assert.Equal(ErrorCode.InvalidQuantity, error.Code);
    }

    [Fact]
    public void Reserve_EventNameIsCaseInsensitive()
    {
        var reserved = Support.ExpectSuccess(Inventory().Reserve("jazz night", 3));

        Assert.Equal(7, RemainingOf(reserved, "Jazz Night"));
    }

    [Fact]
    public void Release_RestoresReservedSeats()
    {
        var reserved = Support.ExpectSuccess(Inventory().Reserve("Jazz Night", 4));

        var released = Support.ExpectSuccess(reserved.Release("Jazz Night", 4));

        Assert.Equal(10, RemainingOf(released, "Jazz Night"));
    }

    [Fact]
    public void Release_NeverReserved_ReturnsInvalidState()
    {
        var error = Support.ExpectFailure(Inventory().Release("Jazz Night", 1));

        Assert.Equal(ErrorCode.InvalidState, error.Code);
        Assert.Equal("Jazz Night", error.Subject);
    }

    [Fact]
    public void Release_MoreThanReserved_ReturnsInvalidState()
    {
        var reserved = Support.ExpectSuccess(Inventory().Reserve("Jazz Night", 2));

        var error = Support.ExpectFailure(reserved.Release("Jazz Night", 3));

        Assert.Equal(ErrorCode.InvalidState, error.Code);
    }

    [Fact]
    public void Release_HugeQuantity_DoesNotOverflow()
    {
        var reserved = Support.ExpectSuccess(Inventory().Reserve("Jazz Night", 2));

        var error = Support.ExpectFailure(reserved.Release("Jazz Night", int.MaxValue));

        Assert.Equal(ErrorCode.InvalidState, error.Code);
    }
}
