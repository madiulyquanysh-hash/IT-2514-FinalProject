namespace TicketBookingSystem.Domain;

public static class BookingQueries
{
    public static IEnumerable<ProcessedBooking> InPeriod(
        this IEnumerable<ProcessedBooking> bookings,
        DateOnly start,
        DateOnly end) =>
        bookings.Where(booking => booking.Entry.BookedOn >= start && booking.Entry.BookedOn <= end);
}