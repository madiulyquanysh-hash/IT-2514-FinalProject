namespace TicketBookingSystem.Domain;

public sealed class ValidatedBooking
{
    public string EventName { get; }
    public int Quantity { get; }
    public decimal TotalAmount { get; }

    private ValidatedBooking(string eventName, int quantity, decimal totalAmount)
    {
        EventName = eventName;
        Quantity = quantity;
        TotalAmount = totalAmount;
    }

    public static Result<ValidatedBooking, AppError> Validate(BookingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventName))
        {
            return Result<ValidatedBooking, AppError>.Fail(AppError.InvalidEventName());
        }

        if (request.Quantity <= 0)
        {
            return Result<ValidatedBooking, AppError>.Fail(AppError.InvalidQuantity(request.EventName));
        }

        if (request.Quantity > request.AvailableCapacity)
        {
            return Result<ValidatedBooking, AppError>.Fail(AppError.CapacityExceeded(request.EventName));
        }

        if (request.UnitPrice <= 0)
        {
            return Result<ValidatedBooking, AppError>.Fail(AppError.InvalidUnitPrice(request.EventName));
        }

        var total = request.Quantity * request.UnitPrice;
        return Result<ValidatedBooking, AppError>.Ok(new ValidatedBooking(request.EventName, request.Quantity, total));
    }

    public static ValidationResult Create(BookingRequest request) =>
        Validate(request).Match<ValidationResult>(
            booking => new ValidationResult.Valid(booking),
            error => new ValidationResult.Invalid(error.Message));
}