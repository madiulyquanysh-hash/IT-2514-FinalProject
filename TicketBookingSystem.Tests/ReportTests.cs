using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class ReportTests
{
    private static ProcessedBooking[] Sample() => new[]
    {
        Support.Processed("B-001", "Tech Conference", 2, 270m, 775, "2026-10-01", discount: 10, customer: "Alice"),
        Support.Processed("B-002", "Jazz Night", 4, 182m, 655, "2026-10-02", customer: "Bob"),
        Support.Processed("B-003", "Tech Conference", 5, 750m, 2125, "2026-10-05", customer: "Carol"),
        Support.Processed("B-004", "Jazz Night", 10, 455m, 1638, "2026-10-07", customer: "Dave"),
        Support.Processed("B-005", "Tech Conference", 1, 135m, 388, "2026-10-09", discount: 10, customer: "Erin"),
        Support.Processed("B-010", "Tech Conference", 3, 450m, 1275, "2026-10-15", customer: "Judy")
    };

    private static readonly Func<ProcessedBooking, long> RevenueFocused = Scoring.CreateScore(new ScoreConfig(1, 10, 2));
    private static readonly Func<ProcessedBooking, long> VolumeFocused = Scoring.CreateScore(new ScoreConfig(10, 1, 0));

    private static HighValueReport HighValue(
        IEnumerable<ProcessedBooking> data,
        string start,
        string end,
        decimal minimum,
        Func<ProcessedBooking, long> score) =>
        Support.ExpectSuccess(BookingReports.HighValue(data, Support.Date(start), Support.Date(end), minimum, score));

    [Fact]
    public void EventRevenue_NormalData_GroupsAndTotals()
    {
        var rows = BookingReports.EventRevenue(Sample());

        Assert.Equal(2, rows.Count);
        Assert.Equal("Tech Conference", rows[0].EventName);
        Assert.Equal(4, rows[0].Bookings);
        Assert.Equal(11, rows[0].Tickets);
        Assert.Equal(1605m, rows[0].Revenue);
        Assert.Equal(4563L, rows[0].ServiceFeeCents);
        Assert.Equal("Jazz Night", rows[1].EventName);
        Assert.Equal(2, rows[1].Bookings);
        Assert.Equal(14, rows[1].Tickets);
        Assert.Equal(637m, rows[1].Revenue);
        Assert.Equal(2293L, rows[1].ServiceFeeCents);
    }

    [Fact]
    public void EventRevenue_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(BookingReports.EventRevenue(Array.Empty<ProcessedBooking>()));
    }

    [Fact]
    public void EventRevenue_TiedRevenue_OrdersByEventName()
    {
        var data = new[]
        {
            Support.Processed("B-1", "Beta", 1, 100m, 10, "2026-10-01"),
            Support.Processed("B-2", "Alpha", 1, 100m, 10, "2026-10-01")
        };

        var rows = BookingReports.EventRevenue(data);

        Assert.Equal(new[] { "Alpha", "Beta" }, rows.Select(r => r.EventName));
    }

    [Fact]
    public void HighValue_NormalData_FiltersOrdersAndTotals()
    {
        var report = HighValue(Sample(), "2026-10-01", "2026-10-14", 200m, RevenueFocused);

        Assert.Equal(new[] { "B-003", "B-004", "B-001" }, report.Rows.Select(r => r.Reference));
        Assert.Equal(new[] { 75L, 50L, 2L }, report.Rows.Select(r => r.Score));
        Assert.Equal(1475m, report.TotalAmount);
        Assert.Equal(4538L, report.TotalServiceFeeCents);
    }

    [Fact]
    public void HighValue_DifferentScoreFunction_ChangesOrder()
    {
        var report = HighValue(Sample(), "2026-10-01", "2026-10-14", 200m, VolumeFocused);

        Assert.Equal(new[] { "B-004", "B-003", "B-001" }, report.Rows.Select(r => r.Reference));
        Assert.Equal(new[] { 104L, 57L, 22L }, report.Rows.Select(r => r.Score));
        Assert.Equal(1475m, report.TotalAmount);
    }

    [Fact]
    public void HighValue_EmptyInput_ReturnsEmptyReport()
    {
        var report = HighValue(Array.Empty<ProcessedBooking>(), "2026-10-01", "2026-10-31", 0m, RevenueFocused);

        Assert.Empty(report.Rows);
        Assert.Equal(0m, report.TotalAmount);
        Assert.Equal(0L, report.TotalServiceFeeCents);
    }

    [Fact]
    public void HighValue_NoMatches_ReturnsEmptyReport()
    {
        var report = HighValue(Sample(), "2026-10-01", "2026-10-31", 100000m, RevenueFocused);

        Assert.Empty(report.Rows);
        Assert.Equal(0m, report.TotalAmount);
    }

    [Fact]
    public void HighValue_TiedScores_OrdersByReference()
    {
        var data = new[]
        {
            Support.Processed("B-9", "Event", 2, 300m, 10, "2026-10-02"),
            Support.Processed("B-1", "Event", 2, 300m, 10, "2026-10-02"),
            Support.Processed("B-5", "Event", 2, 300m, 10, "2026-10-02")
        };

        var report = HighValue(data, "2026-10-01", "2026-10-31", 0m, RevenueFocused);

        Assert.Equal(new[] { "B-1", "B-5", "B-9" }, report.Rows.Select(r => r.Reference));
    }

    [Fact]
    public void HighValue_PeriodBoundaries_AreInclusive()
    {
        var data = new[]
        {
            Support.Processed("B-1", "Event", 1, 100m, 1, "2026-10-01"),
            Support.Processed("B-2", "Event", 1, 100m, 1, "2026-10-14"),
            Support.Processed("B-3", "Event", 1, 100m, 1, "2026-10-15")
        };

        var report = HighValue(data, "2026-10-01", "2026-10-14", 0m, RevenueFocused);

        Assert.Equal(new[] { "B-1", "B-2" }, report.Rows.Select(r => r.Reference).OrderBy(r => r, StringComparer.Ordinal));
    }

    [Fact]
    public void HighValue_InvalidParameters_ReturnFailure()
    {
        var reversed = BookingReports.HighValue(Sample(), Support.Date("2026-10-14"), Support.Date("2026-10-01"), 0m, RevenueFocused);
        var negative = BookingReports.HighValue(Sample(), Support.Date("2026-10-01"), Support.Date("2026-10-14"), -1m, RevenueFocused);

        Assert.Equal(ErrorCode.InvalidReportParameters, Support.ExpectFailure(reversed).Code);
        Assert.Equal(ErrorCode.InvalidReportParameters, Support.ExpectFailure(negative).Code);
    }

    [Fact]
    public void Formatter_EventRevenueLines_ProducesCsv()
    {
        var lines = ReportFormatter.EventRevenueLines(BookingReports.EventRevenue(Sample()));

        Assert.Equal("event,bookings,tickets,revenue,service_fee", lines[0]);
        Assert.Equal("Tech Conference,4,11,1605.00,45.63", lines[1]);
        Assert.Equal("Jazz Night,2,14,637.00,22.93", lines[2]);
    }

    [Fact]
    public void Formatter_HighValueLines_IncludesTotalRow()
    {
        var report = HighValue(Sample(), "2026-10-01", "2026-10-14", 200m, RevenueFocused);

        var lines = ReportFormatter.HighValueLines(report);

        Assert.Equal("B-003,Carol,Tech Conference,750.00,21.25,75", lines[1]);
        Assert.Equal("TOTAL,,,1475.00,45.38,", lines[^1]);
    }

    [Fact]
    public void Formatter_EscapesCommasAndQuotes()
    {
        var rows = new[]
        {
            new EventRevenueRow("Rock, Paper", 1, 1, 10m, 100),
            new EventRevenueRow("Say \"Hi\"", 1, 1, 10m, 100)
        };

        var lines = ReportFormatter.EventRevenueLines(rows);

        Assert.Equal("\"Rock, Paper\",1,1,10.00,1.00", lines[1]);
        Assert.Equal("\"Say \"\"Hi\"\"\",1,1,10.00,1.00", lines[2]);
    }
}