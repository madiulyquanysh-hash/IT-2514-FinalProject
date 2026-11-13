using System.Collections.Immutable;

namespace TicketBookingSystem.Domain;

public sealed class BookingLedger
{
    private readonly ImmutableDictionary<string, LedgerEntry> entries;

    private BookingLedger(ImmutableDictionary<string, LedgerEntry> entries)
    {
        this.entries = entries;
    }

    public static BookingLedger From(IEnumerable<ProcessedBooking> bookings) =>
        new(bookings.ToImmutableDictionary(
            booking => booking.Entry.Reference,
            booking => new LedgerEntry(booking, new BookingStatus.Active()),
            StringComparer.Ordinal));

    public IReadOnlyList<LedgerEntry> Entries =>
        entries.Values.OrderBy(entry => entry.Booking.Entry.Reference, StringComparer.Ordinal).ToArray();

    public IReadOnlyList<ProcessedBooking> ActiveBookings =>
        Entries
            .Where(entry => entry.Status is BookingStatus.Active)
            .Select(entry => entry.Booking)
            .ToArray();

    public Maybe<LedgerEntry> Find(string reference) =>
        entries.TryGetValue(reference, out var entry)
            ? Maybe.Some(entry)
            : Maybe.None<LedgerEntry>();

    public BookingLedger With(LedgerEntry entry) =>
        new(entries.SetItem(entry.Booking.Entry.Reference, entry));
}
