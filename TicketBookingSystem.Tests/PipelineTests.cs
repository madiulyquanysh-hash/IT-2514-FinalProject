using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class PipelineTests
{
    private static PipelineResult Run(PipelineInput input) =>
        Support.ExpectSuccess(BookingPipeline.Run(input));

    [Fact]
    public void Run_NormalData_ProducesEveryStageOutput()
    {
        var result = Run(Samples.Input(new StubFeeCalculator(100)));

        Assert.Equal(
            new[] { "B-001", "B-002", "B-003", "B-004", "B-005", "B-010", "B-011" },
            result.Batch.Accepted.Select(b => b.Entry.Reference));
        Assert.Equal(
            new[] { "B-006", "B-007", "B-008", "B-009" },
            result.Batch.Rejected.Select(r => r.Entry.Reference));
        Assert.Equal(
            new[] { ErrorCode.CapacityExceeded, ErrorCode.EventNotFound, ErrorCode.InvalidQuantity, ErrorCode.PromoCodeNotFound },
            result.Batch.Rejected.Select(r => r.Error.Code));
        Assert.Equal(
            new[] { "B-001", "B-002", "B-003", "B-004", "B-005", "B-010" },
            result.Allocation.Allocated.Select(b => b.Entry.Reference));
        Assert.Equal(new[] { "B-011" }, result.Allocation.Waitlisted.Select(b => b.Entry.Reference));
        Assert.Equal(new[] { "B-001", "B-003" }, result.Cancellations.Receipts.Select(r => r.Reference));
        Assert.Equal(new[] { 270m, 375m }, result.Cancellations.Receipts.Select(r => r.RefundAmount));
        Assert.Equal(
            new[] { ErrorCode.InvalidCancellationDate, ErrorCode.BookingNotFound, ErrorCode.InvalidState },
            result.Cancellations.Rejected.Select(r => r.Error.Code));
    }

    [Fact]
    public void Run_FinalInventory_ReflectsAllocationAndReleasedSeats()
    {
        var result = Run(Samples.Input(new StubFeeCalculator(100)));

        Assert.Equal(
            new[] { new SeatLevel("Jazz Night", 10, 0), new SeatLevel("Sold Out Gala", 0, 0), new SeatLevel("Tech Conference", 50, 39) },
            result.Allocation.Inventory.Levels);
        Assert.Equal(
            new[] { new SeatLevel("Jazz Night", 10, 0), new SeatLevel("Sold Out Gala", 0, 0), new SeatLevel("Tech Conference", 50, 46) },
            result.Inventory.Levels);
    }

    [Fact]
    public void Run_ReportsUseOnlyActiveBookings()
    {
        var result = Run(Samples.Input(new StubFeeCalculator(100)));

        Assert.Equal(new[] { "Tech Conference", "Jazz Night" }, result.Revenue.Select(r => r.EventName));
        Assert.Equal(new[] { 585m, 455m }, result.Revenue.Select(r => r.Revenue));
        Assert.Equal(new[] { 4, 10 }, result.Revenue.Select(r => r.Tickets));
        Assert.Equal(new[] { 200L, 200L }, result.Revenue.Select(r => r.ServiceFeeCents));
        Assert.Equal(new[] { "B-010", "B-004", "B-002", "B-005" }, result.HighValue.Rows.Select(r => r.Reference));
        Assert.Equal(new[] { 43L, 26L, 14L, -9L }, result.HighValue.Rows.Select(r => r.Score));
        Assert.Equal(1040m, result.HighValue.TotalAmount);
        Assert.Equal(400L, result.HighValue.TotalServiceFeeCents);
    }

    [Fact]
    public void Run_WaitlistedBooking_CannotBeCancelled()
    {
        var cancellations = new[] { Samples.Cancellation("B-011", "2026-10-17") };

        var result = Run(Samples.Input(new StubFeeCalculator(100), null, null, cancellations));

        Assert.Empty(result.Cancellations.Receipts);
        Assert.Equal(ErrorCode.BookingNotFound, result.Cancellations.Rejected[0].Error.Code);
    }

    [Fact]
    public void Run_FeeFailure_FailsBeforeLaterStages()
    {
        var error = Support.ExpectFailure(BookingPipeline.Run(Samples.Input(new FailingFeeCalculator())));

        Assert.Equal(ErrorCode.NativeLibraryUnavailable, error.Code);
        Assert.Equal(ErrorCategory.Operational, error.Category);
    }

    [Fact]
    public void Run_InvalidPeriod_FailsInReportStage()
    {
        var settings = Samples.Settings() with { PeriodStart = Support.Date("2026-11-01") };

        var error = Support.ExpectFailure(BookingPipeline.Run(Samples.Input(new StubFeeCalculator(100), settings)));

        Assert.Equal(ErrorCode.InvalidReportParameters, error.Code);
    }

    [Fact]
    public void Run_EmptyInputs_ReturnsEmptyResults()
    {
        var input = Samples.Input(new StubFeeCalculator(100), null, Array.Empty<BookingEntry>(), Array.Empty<CancellationEntry>());

        var result = Run(input);

        Assert.Empty(result.Batch.Accepted);
        Assert.Empty(result.Allocation.Allocated);
        Assert.Empty(result.Cancellations.Receipts);
        Assert.Empty(result.Revenue);
        Assert.Empty(result.HighValue.Rows);
        Assert.All(result.Inventory.Levels, level => Assert.Equal(level.Capacity, level.Remaining));
    }

    [Fact]
    public void Run_RefundPolicyChange_ChangesOnlyRefunds()
    {
        var fees = new StubFeeCalculator(100);

        var flexible = Run(Samples.Input(fees, Samples.Settings(null, RefundPolicies.Flexible)));
        var strict = Run(Samples.Input(fees, Samples.Settings(null, RefundPolicies.Strict)));

        Assert.Equal(new[] { 270m, 375m }, flexible.Cancellations.Receipts.Select(r => r.RefundAmount));
        Assert.Equal(new[] { 270m, 0m }, strict.Cancellations.Receipts.Select(r => r.RefundAmount));
        Assert.Equal(
            flexible.Allocation.Allocated.Select(b => b.Entry.Reference),
            strict.Allocation.Allocated.Select(b => b.Entry.Reference));
    }

    [Fact]
    public void Run_DoesNotChangeInputCollections()
    {
        var bookings = Samples.Bookings();
        var cancellations = Samples.Cancellations();
        var bookingsSnapshot = bookings.ToArray();
        var cancellationsSnapshot = cancellations.ToArray();

        _ = Run(Samples.Input(new StubFeeCalculator(100), null, bookings, cancellations));

        Assert.Equal(bookingsSnapshot, bookings);
        Assert.Equal(cancellationsSnapshot, cancellations);
    }

    [Fact]
    public void Stages_CanBeComposedStepByStep()
    {
        var input = Samples.Input(new StubFeeCalculator(100));

        var composed = BookingPipeline.ProcessBookings(input)
            .Map(stage => BookingPipeline.AllocateSeats(stage))
            .Bind(stage => BookingPipeline.ApplyCancellations(stage))
            .Bind(stage => BookingPipeline.BuildReports(stage));

        var direct = Run(input);

        Assert.Equal(
            direct.HighValue.Rows.Select(r => r.Reference),
            Support.ExpectSuccess(composed).HighValue.Rows.Select(r => r.Reference));
    }
}
