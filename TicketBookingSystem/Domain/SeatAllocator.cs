using System.Collections.Immutable;

namespace TicketBookingSystem.Domain;

public sealed record AllocationResult(
    IReadOnlyList<ProcessedBooking> Allocated,
    IReadOnlyList<RejectedBooking> Waitlisted,
    SeatInventory Inventory);

public static class SeatAllocator
{
    public static AllocationResult Allocate(IEnumerable<ProcessedBooking> bookings, SeatInventory inventory)
    {
        var ordered = bookings
            .OrderBy(booking => booking.Entry.BookedOn)
            .ThenBy(booking => booking.Entry.Reference, StringComparer.Ordinal);

        var seed = new AllocationState(
            inventory,
            ImmutableList<ProcessedBooking>.Empty,
            ImmutableList<RejectedBooking>.Empty);

        var final = ordered.Aggregate(seed, Step);
        return new AllocationResult(final.Allocated, final.Waitlisted, final.Inventory);
    }

    private static AllocationState Step(AllocationState state, ProcessedBooking booking) =>
        state.Inventory.Reserve(booking.Receipt.EventName, booking.Receipt.Quantity).Match(
            reserved => state with { Inventory = reserved, Allocated = state.Allocated.Add(booking) },
            error => state with { Waitlisted = state.Waitlisted.Add(new RejectedBooking(booking.Entry, error)) });

    private sealed record AllocationState(
        SeatInventory Inventory,
        ImmutableList<ProcessedBooking> Allocated,
        ImmutableList<RejectedBooking> Waitlisted);
}
