namespace TicketBookingSystem.Domain;

public sealed record BookingEntry(
    string Reference,
    string Customer,
    string EventName,
    int Quantity,
    string? PromoCode,
    DateOnly BookedOn);

public sealed record ProcessedBooking(BookingEntry Entry, BookingReceipt Receipt, long ServiceFeeCents);

public sealed record RejectedBooking(BookingEntry Entry, AppError Error);

public sealed record BookingBatch(IReadOnlyList<ProcessedBooking> Accepted, IReadOnlyList<RejectedBooking> Rejected);