namespace TicketBookingSystem.Domain;

public sealed class BookingService
{
    private readonly EventCatalog catalog;
    private readonly PromoCatalog promos;

    public BookingService(EventCatalog catalog, PromoCatalog promos)
    {
        this.catalog = catalog;
        this.promos = promos;
    }

    public Result<PricedBooking, AppError> Prepare(BookingCommand command) =>
        FindEvent(command)
            .Bind(info => ValidateRequest(info, command))
            .Bind(booking => PriceBooking(booking, command));

    public Result<BookingReceipt, AppError> Confirm(PricedBooking priced, BookingState current) =>
        BookingWorkflow.TryConfirm(current, priced.Booking)
            .Map(confirmed => new BookingReceipt(
                priced.Booking.EventName,
                priced.Booking.Quantity,
                priced.DiscountPercent,
                priced.FinalAmount,
                confirmed.ConfirmedAt));

    public Result<BookingReceipt, AppError> Book(BookingCommand command) =>
        Prepare(command)
            .Bind(priced => Confirm(priced, new BookingState.Pending(priced.Booking)));

    private Result<EventInfo, AppError> FindEvent(BookingCommand command) =>
        catalog.Find(command.EventName)
            .ToResult(() => AppError.EventNotFound(command.EventName));

    private static Result<ValidatedBooking, AppError> ValidateRequest(EventInfo info, BookingCommand command) =>
        ValidatedBooking.Validate(
            new BookingRequest(info.Name, command.Quantity, info.UnitPrice, info.AvailableCapacity));

    private Result<PricedBooking, AppError> PriceBooking(ValidatedBooking booking, BookingCommand command) =>
        ResolveDiscount(command.PromoCode)
            .Map(percent => Price(booking, percent));

    private Result<int, AppError> ResolveDiscount(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? Result<int, AppError>.Ok(0)
            : promos.Find(code).ToResult(() => AppError.PromoCodeNotFound(code));

    private static PricedBooking Price(ValidatedBooking booking, int discountPercent)
    {
        var factor = (100m - discountPercent) / 100m;
        var finalAmount = decimal.Round(booking.TotalAmount * factor, 2, MidpointRounding.AwayFromZero);
        return new PricedBooking(booking, discountPercent, finalAmount);
    }
}
