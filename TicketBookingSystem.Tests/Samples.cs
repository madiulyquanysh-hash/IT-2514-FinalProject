using TicketBookingSystem.Domain;

namespace TicketBookingSystem.Tests;

internal static class Samples
{
    public static EventCatalog Events() =>
        new(new[]
        {
            new EventInfo("Tech Conference", 150m, 50),
            new EventInfo("Jazz Night", 45.5m, 10),
            new EventInfo("Sold Out Gala", 300m, 0)
        });

    public static PromoCatalog Promos() =>
        new(new[]
        {
            new PromoCode("WELCOME10", 10),
            new PromoCode("NODISCOUNT", 0)
        });

    public static PipelineSettings Settings(
        Func<ProcessedBooking, long>? score = null,
        Func<int, int>? refundPolicy = null) =>
        new(
            Support.Date("2026-10-01"),
            Support.Date("2026-10-31"),
            100m,
            score ?? Scoring.CreateScore(new ScoreConfig(1, 10, 2)),
            refundPolicy ?? RefundPolicies.Flexible);

    public static BookingEntry[] Bookings() =>
        new[]
        {
            Support.Entry("B-001", "Tech Conference", 2, "WELCOME10", "2026-10-01", "Alice"),
            Support.Entry("B-002", "Jazz Night", 4, null, "2026-10-02", "Bob"),
            Support.Entry("B-003", "Tech Conference", 5, null, "2026-10-05", "Carol"),
            Support.Entry("B-004", "Jazz Night", 6, "NODISCOUNT", "2026-10-07", "Dave"),
            Support.Entry("B-005", "Tech Conference", 1, "WELCOME10", "2026-10-09", "Erin"),
            Support.Entry("B-006", "Sold Out Gala", 1, null, "2026-10-10", "Frank"),
            Support.Entry("B-007", "Unknown Event", 2, null, "2026-10-11", "Grace"),
            Support.Entry("B-008", "Jazz Night", 0, null, "2026-10-12", "Heidi"),
            Support.Entry("B-009", "Jazz Night", 3, "BOGUS", "2026-10-13", "Ivan"),
            Support.Entry("B-010", "Tech Conference", 3, "NODISCOUNT", "2026-10-15", "Judy"),
            Support.Entry("B-011", "Jazz Night", 2, null, "2026-10-16", "Kate")
        };

    public static CancellationEntry Cancellation(string reference, string date, string reason = "reason") =>
        new(reference, Support.Date(date), reason);

    public static CancellationEntry[] Cancellations() =>
        new[]
        {
            Cancellation("B-001", "2026-10-03", "Change of plans"),
            Cancellation("B-003", "2026-10-15", "Schedule conflict"),
            Cancellation("B-001", "2026-10-20", "Duplicate request"),
            Cancellation("B-099", "2026-10-04", "Unknown booking"),
            Cancellation("B-005", "2026-10-01", "Typo in date")
        };

    public static PipelineInput Input(
        IFeeCalculator fees,
        PipelineSettings? settings = null,
        BookingEntry[]? bookings = null,
        CancellationEntry[]? cancellations = null) =>
        new(
            Events(),
            Promos(),
            bookings ?? Bookings(),
            cancellations ?? Cancellations(),
            fees,
            settings ?? Settings());
}
