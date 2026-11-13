using System.Globalization;

namespace TicketBookingSystem.Domain;

public sealed record BookingCommand(string EventName, int Quantity, string? PromoCode = null);

public sealed record PricedBooking(ValidatedBooking Booking, int DiscountPercent, decimal FinalAmount);

public sealed record BookingReceipt(string EventName, int Quantity, int DiscountPercent, decimal FinalAmount, DateTime ConfirmedAt)
{
    public string Describe() =>
        $"{Quantity} x {EventName}, discount {DiscountPercent}%, total {FinalAmount.ToString("F2", CultureInfo.InvariantCulture)}";
}