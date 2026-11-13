using System.Globalization;

namespace TicketBookingSystem.Domain;

public static class ReportFormatter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static IReadOnlyList<string> EventRevenueLines(IReadOnlyList<EventRevenueRow> rows)
    {
        var lines = new List<string> { "event,bookings,tickets,revenue,service_fee" };
        lines.AddRange(rows.Select(row => string.Join(",",
            Csv(row.EventName),
            row.Bookings.ToString(Invariant),
            row.Tickets.ToString(Invariant),
            Money(row.Revenue),
            Cents(row.ServiceFeeCents))));
        return lines;
    }

    public static IReadOnlyList<string> HighValueLines(HighValueReport report)
    {
        var lines = new List<string> { "reference,customer,event,amount,service_fee,score" };
        lines.AddRange(report.Rows.Select(row => string.Join(",",
            Csv(row.Reference),
            Csv(row.Customer),
            Csv(row.EventName),
            Money(row.Amount),
            Cents(row.ServiceFeeCents),
            row.Score.ToString(Invariant))));
        lines.Add(string.Join(",", "TOTAL", string.Empty, string.Empty, Money(report.TotalAmount), Cents(report.TotalServiceFeeCents), string.Empty));
        return lines;
    }

    public static IReadOnlyList<string> CancellationLines(IReadOnlyList<CancellationReceipt> receipts)
    {
        var lines = new List<string> { "reference,customer,event,tickets,days_since_booking,refund_percent,refund,retained,reason" };
        lines.AddRange(receipts.Select(receipt => string.Join(",",
            Csv(receipt.Reference),
            Csv(receipt.Customer),
            Csv(receipt.EventName),
            receipt.Quantity.ToString(Invariant),
            receipt.DaysSinceBooking.ToString(Invariant),
            receipt.RefundPercent.ToString(Invariant),
            Money(receipt.RefundAmount),
            Money(receipt.RetainedAmount),
            Csv(receipt.Reason))));
        return lines;
    }

    public static IReadOnlyList<string> RejectedLines(IEnumerable<RejectedBooking> rejected)
    {
        var lines = new List<string> { "reference,customer,event,error_code" };
        lines.AddRange(rejected.Select(item => string.Join(",",
            Csv(item.Entry.Reference),
            Csv(item.Entry.Customer),
            Csv(item.Entry.EventName),
            item.Error.Code.ToString())));
        return lines;
    }

    public static IReadOnlyList<string> SeatLines(SeatInventory inventory)
    {
        var lines = new List<string> { "event,capacity,remaining" };
        lines.AddRange(inventory.Levels.Select(level => string.Join(",",
            Csv(level.EventName),
            level.Capacity.ToString(Invariant),
            level.Remaining.ToString(Invariant))));
        return lines;
    }

    public static IReadOnlyList<string> CombinedLines(PipelineResult result)
    {
        var lines = new List<string> { "Event revenue" };
        lines.AddRange(EventRevenueLines(result.Revenue));
        lines.Add(string.Empty);
        lines.Add("High-value bookings");
        lines.AddRange(HighValueLines(result.HighValue));
        lines.Add(string.Empty);
        lines.Add("Cancellations and refunds");
        lines.AddRange(CancellationLines(result.Cancellations.Receipts));
        lines.Add(string.Empty);
        lines.Add("Rejected and waitlisted bookings");
        lines.AddRange(RejectedLines(result.Batch.Rejected.Concat(result.Allocation.Waitlisted)));
        lines.Add(string.Empty);
        lines.Add("Seat inventory");
        lines.AddRange(SeatLines(result.Inventory));
        return lines;
    }

    private static string Money(decimal value) => value.ToString("F2", Invariant);

    private static string Cents(long cents) => (cents / 100m).ToString("F2", Invariant);

    private static string Csv(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0
            ? value
            : "\"" + value.Replace("\"", "\"\"") + "\"";
}
