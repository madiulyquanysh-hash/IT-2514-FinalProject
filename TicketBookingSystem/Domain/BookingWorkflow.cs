namespace TicketBookingSystem.Domain;

public static class BookingWorkflow
{
    public static BookingState Process(ValidationResult validationResult, BookingState currentState)
    {
        return validationResult switch
        {
            ValidationResult.Invalid reason => new BookingState.Cancelled(reason.Reason),
            ValidationResult.Valid valid => currentState switch
            {
                BookingState.Confirmed => currentState,
                BookingState.Pending _ => new BookingState.Confirmed(valid.Booking, DateTime.UtcNow),
                BookingState.Cancelled => new BookingState.Cancelled("Cannot confirm a cancelled workflow."),
                _ => new BookingState.Confirmed(valid.Booking, DateTime.UtcNow)
            },
            _ => new BookingState.Cancelled("Unknown validation state.")
        };
    }

    public static Result<BookingState.Confirmed, AppError> TryConfirm(BookingState current, ValidatedBooking booking)
    {
        return current switch
        {
            BookingState.Pending =>
                Result<BookingState.Confirmed, AppError>.Ok(new BookingState.Confirmed(booking, DateTime.UtcNow)),
            BookingState.Confirmed =>
                Result<BookingState.Confirmed, AppError>.Fail(
                    AppError.InvalidState("Booking is already confirmed.", "Confirmed")),
            BookingState.Cancelled cancelled =>
                Result<BookingState.Confirmed, AppError>.Fail(
                    AppError.InvalidState($"Cannot confirm a cancelled booking: {cancelled.Reason}", "Cancelled")),
            _ =>
                Result<BookingState.Confirmed, AppError>.Fail(
                    AppError.InvalidState("Booking is in an unknown state.", "Unknown"))
        };
    }
}