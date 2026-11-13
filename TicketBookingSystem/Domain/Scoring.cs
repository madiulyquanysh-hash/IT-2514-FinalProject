namespace TicketBookingSystem.Domain;

public sealed record ScoreConfig(int PointsPerTicket, int PointsPerHundredUnits, int PenaltyPerDiscountPercent);

public static class Scoring
{
    public static readonly Func<ScoreConfig, Func<ProcessedBooking, long>> CreateScore =
        config => booking =>
            (long)booking.Receipt.Quantity * config.PointsPerTicket
            + (long)decimal.Truncate(booking.Receipt.FinalAmount / 100m) * config.PointsPerHundredUnits
            - (long)booking.Receipt.DiscountPercent * config.PenaltyPerDiscountPercent;
}