namespace TicketBookingSystem.Domain;

public sealed record EventRevenueRow(string EventName, int Bookings, int Tickets, decimal Revenue, long ServiceFeeCents);

public sealed record HighValueRow(
    string Reference,
    string Customer,
    string EventName,
    decimal Amount,
    long ServiceFeeCents,
    long Score);

public sealed record HighValueReport(IReadOnlyList<HighValueRow> Rows, decimal TotalAmount, long TotalServiceFeeCents);

public static class BookingReports
{
    public static IReadOnlyList<EventRevenueRow> EventRevenue(IEnumerable<ProcessedBooking> bookings) =>
        bookings
            .GroupBy(booking => booking.Receipt.EventName)
            .Select(group => new EventRevenueRow(
                group.Key,
                group.Count(),
                group.Sum(booking => booking.Receipt.Quantity),
                group.Sum(booking => booking.Receipt.FinalAmount),
                group.Sum(booking => booking.ServiceFeeCents)))
            .OrderByDescending(row => row.Revenue)
            .ThenBy(row => row.EventName, StringComparer.Ordinal)
            .ToArray();

    public static Result<HighValueReport, AppError> HighValue(
        IEnumerable<ProcessedBooking> bookings,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal minimumAmount,
        Func<ProcessedBooking, long> score)
    {
        if (periodStart > periodEnd)
        {
            return Result<HighValueReport, AppError>.Fail(
                AppError.InvalidReportParameters("The period start must not be after the period end."));
        }

        if (minimumAmount < 0m)
        {
            return Result<HighValueReport, AppError>.Fail(
                AppError.InvalidReportParameters("The minimum amount must not be negative."));
        }

        var rows = bookings
            .InPeriod(periodStart, periodEnd)
            .Where(booking => booking.Receipt.FinalAmount >= minimumAmount)
            .Select(booking => new HighValueRow(
                booking.Entry.Reference,
                booking.Entry.Customer,
                booking.Receipt.EventName,
                booking.Receipt.FinalAmount,
                booking.ServiceFeeCents,
                score(booking)))
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.Reference, StringComparer.Ordinal)
            .ToArray();

        var totalAmount = rows.Aggregate(0m, (sum, row) => sum + row.Amount);
        var totalFee = rows.Aggregate(0L, (sum, row) => sum + row.ServiceFeeCents);

        return Result<HighValueReport, AppError>.Ok(new HighValueReport(rows, totalAmount, totalFee));
    }
}