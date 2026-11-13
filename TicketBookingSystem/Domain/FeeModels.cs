namespace TicketBookingSystem.Domain;

public sealed record FeeSchedule(int RateBasisPoints, int PerTicketCents)
{
    public static FeeSchedule Standard { get; } = new(250, 50);
}

public static class FeeLimits
{
    public const long MaxAmountCents = 1_000_000_000_000L;
    public const int MaxQuantity = 1_000_000;
    public const int MaxRateBasisPoints = 10_000;
    public const int MaxPerTicketCents = 100_000;
}

public interface IFeeCalculator
{
    Result<long, AppError> CalculateFeeCents(decimal amount, int quantity);
}