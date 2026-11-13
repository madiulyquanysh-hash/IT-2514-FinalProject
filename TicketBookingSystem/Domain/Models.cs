namespace TicketBookingSystem.Domain;

public abstract record ValidationResult
{
    private ValidationResult() { }
    public sealed record Valid(ValidatedBooking Booking) : ValidationResult;
    public sealed record Invalid(string Reason) : ValidationResult;
}

public abstract record BookingState
{
    private BookingState() { }
    public sealed record Pending(ValidatedBooking Booking) : BookingState;
    public sealed record Confirmed(ValidatedBooking Booking, DateTime ConfirmedAt) : BookingState;
    public sealed record Cancelled(string Reason) : BookingState;
}

public record BookingRequest(string EventName, int Quantity, decimal UnitPrice, int AvailableCapacity);