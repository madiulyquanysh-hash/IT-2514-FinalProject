namespace TicketBookingSystem.Domain;

public sealed record CancellationEntry(string Reference, DateOnly CancelledOn, string Reason);

public sealed record CancellationReceipt(
    string Reference,
    string Customer,
    string EventName,
    int Quantity,
    int DaysSinceBooking,
    int RefundPercent,
    decimal RefundAmount,
    decimal RetainedAmount,
    string Reason);

public sealed record RejectedCancellation(CancellationEntry Entry, AppError Error);

public abstract record BookingStatus
{
    private BookingStatus() { }

    public sealed record Active : BookingStatus;

    public sealed record Cancelled(DateOnly On, string Reason) : BookingStatus;
}

public sealed record LedgerEntry(ProcessedBooking Booking, BookingStatus Status);
