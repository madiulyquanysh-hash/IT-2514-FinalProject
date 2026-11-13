using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class CancellationTests
{
    private static BookingLedger Ledger() =>
        BookingLedger.From(new[]
        {
            Support.Processed("B-1", "Tech Conference", 2, 270m, 775, "2026-10-01", discount: 10, customer: "Alice"),
            Support.Processed("B-2", "Tech Conference", 5, 750m, 2125, "2026-10-05", customer: "Carol")
        });

    private static BookingStatus StatusOf(BookingLedger ledger, string reference) =>
        Assert.IsType<Maybe<LedgerEntry>.Some>(ledger.Find(reference)).Value.Status;

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 100)]
    [InlineData(7, 100)]
    [InlineData(8, 50)]
    [InlineData(14, 50)]
    [InlineData(15, 0)]
    public void FlexiblePolicy_MapsDaysToPercent(int days, int expected)
    {
        Assert.Equal(expected, RefundPolicies.Flexible(days));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 100)]
    [InlineData(2, 100)]
    [InlineData(3, 0)]
    public void StrictPolicy_MapsDaysToPercent(int days, int expected)
    {
        Assert.Equal(expected, RefundPolicies.Strict(days));
    }

    [Fact]
    public void Cancel_SameDayAsBooking_RefundsEverything()
    {
        var outcome = Support.ExpectSuccess(
            CancellationService.Cancel(Ledger(), Samples.Cancellation("B-1", "2026-10-01"), RefundPolicies.Flexible));

        Assert.Equal(0, outcome.Receipt.DaysSinceBooking);
        Assert.Equal(100, outcome.Receipt.RefundPercent);
        Assert.Equal(270m, outcome.Receipt.RefundAmount);
        Assert.Equal(0m, outcome.Receipt.RetainedAmount);
    }

    [Fact]
    public void Cancel_PartialWindow_RefundsHalf()
    {
        var outcome = Support.ExpectSuccess(
            CancellationService.Cancel(Ledger(), Samples.Cancellation("B-2", "2026-10-15"), RefundPolicies.Flexible));

        Assert.Equal(10, outcome.Receipt.DaysSinceBooking);
        Assert.Equal(50, outcome.Receipt.RefundPercent);
        Assert.Equal(375m, outcome.Receipt.RefundAmount);
        Assert.Equal(375m, outcome.Receipt.RetainedAmount);
        Assert.Equal(750m, outcome.Receipt.RefundAmount + outcome.Receipt.RetainedAmount);
    }

    [Fact]
    public void Cancel_DifferentPolicy_ChangesRefund()
    {
        var request = Samples.Cancellation("B-2", "2026-10-15");

        var flexible = Support.ExpectSuccess(CancellationService.Cancel(Ledger(), request, RefundPolicies.Flexible));
        var strict = Support.ExpectSuccess(CancellationService.Cancel(Ledger(), request, RefundPolicies.Strict));

        Assert.Equal(375m, flexible.Receipt.RefundAmount);
        Assert.Equal(0m, strict.Receipt.RefundAmount);
    }

    [Fact]
    public void Cancel_Success_ReturnsNewLedgerAndKeepsOriginal()
    {
        var original = Ledger();

        var outcome = Support.ExpectSuccess(
            CancellationService.Cancel(original, Samples.Cancellation("B-1", "2026-10-03", "Change of plans"), RefundPolicies.Flexible));

        var cancelled = Assert.IsType<BookingStatus.Cancelled>(StatusOf(outcome.Ledger, "B-1"));
        Assert.Equal("Change of plans", cancelled.Reason);
        Assert.IsType<BookingStatus.Active>(StatusOf(original, "B-1"));
    }

    [Fact]
    public void Cancel_UnknownReference_ReturnsBookingNotFound()
    {
        var error = Support.ExpectFailure(
            CancellationService.Cancel(Ledger(), Samples.Cancellation("B-99", "2026-10-03"), RefundPolicies.Flexible));

        Assert.Equal(ErrorCode.BookingNotFound, error.Code);
        Assert.Equal("B-99", error.Subject);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_ReturnsInvalidState()
    {
        var first = Support.ExpectSuccess(
            CancellationService.Cancel(Ledger(), Samples.Cancellation("B-1", "2026-10-03"), RefundPolicies.Flexible));

        var error = Support.ExpectFailure(
            CancellationService.Cancel(first.Ledger, Samples.Cancellation("B-1", "2026-10-04"), RefundPolicies.Flexible));

        Assert.Equal(ErrorCode.InvalidState, error.Code);
        Assert.Equal("Cancelled", error.Subject);
    }

    [Fact]
    public void Cancel_BeforeBookingDate_ReturnsInvalidCancellationDate()
    {
        var error = Support.ExpectFailure(
            CancellationService.Cancel(Ledger(), Samples.Cancellation("B-1", "2026-09-30"), RefundPolicies.Flexible));

        Assert.Equal(ErrorCode.InvalidCancellationDate, error.Code);
        Assert.Equal("B-1", error.Subject);
    }

    [Theory]
    [InlineData(150)]
    [InlineData(-1)]
    public void Cancel_PolicyReturningInvalidPercent_ReturnsFailure(int percent)
    {
        var error = Support.ExpectFailure(
            CancellationService.Cancel(Ledger(), Samples.Cancellation("B-1", "2026-10-03"), _ => percent));

        Assert.Equal(ErrorCode.InvalidRefundPercent, error.Code);
    }

    [Fact]
    public void Cancel_UnknownReference_DoesNotInvokePolicy()
    {
        var calls = 0;

        _ = CancellationService.Cancel(Ledger(), Samples.Cancellation("B-99", "2026-10-03"), days =>
        {
            calls++;
            return 100;
        });

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Cancel_AlreadyCancelled_DoesNotInvokePolicy()
    {
        var first = Support.ExpectSuccess(
            CancellationService.Cancel(Ledger(), Samples.Cancellation("B-1", "2026-10-03"), RefundPolicies.Flexible));
        var calls = 0;

        _ = CancellationService.Cancel(first.Ledger, Samples.Cancellation("B-1", "2026-10-04"), days =>
        {
            calls++;
            return 100;
        });

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Ledger_Find_PresentAndAbsent()
    {
        var ledger = Ledger();

        Assert.IsType<Maybe<LedgerEntry>.Some>(ledger.Find("B-1"));
        Assert.IsType<Maybe<LedgerEntry>.None>(ledger.Find("B-99"));
    }

    [Fact]
    public void Ledger_ActiveBookings_ExcludeCancelledAndAreOrdered()
    {
        var outcome = Support.ExpectSuccess(
            CancellationService.Cancel(Ledger(), Samples.Cancellation("B-1", "2026-10-03"), RefundPolicies.Flexible));

        Assert.Equal(new[] { "B-2" }, outcome.Ledger.ActiveBookings.Select(b => b.Entry.Reference));
        Assert.Equal(new[] { "B-1", "B-2" }, Ledger().ActiveBookings.Select(b => b.Entry.Reference));
    }

    [Fact]
    public void Run_ProcessesInDateOrderAndCollectsRejected()
    {
        var entries = new[]
        {
            Samples.Cancellation("B-2", "2026-10-15"),
            Samples.Cancellation("B-9", "2026-10-04"),
            Samples.Cancellation("B-1", "2026-10-03")
        };

        var batch = CancellationBatchProcessor.Run(Ledger(), entries, RefundPolicies.Flexible);

        Assert.Equal(new[] { "B-1", "B-2" }, batch.Receipts.Select(r => r.Reference));
        Assert.Equal(new[] { "B-9" }, batch.Rejected.Select(r => r.Entry.Reference));
        Assert.Equal(ErrorCode.BookingNotFound, batch.Rejected[0].Error.Code);
    }

    [Fact]
    public void Run_DuplicateCancellation_RejectsSecond()
    {
        var entries = new[]
        {
            Samples.Cancellation("B-1", "2026-10-03"),
            Samples.Cancellation("B-1", "2026-10-04")
        };

        var batch = CancellationBatchProcessor.Run(Ledger(), entries, RefundPolicies.Flexible);

        Assert.Single(batch.Receipts);
        Assert.Single(batch.Rejected);
        Assert.Equal(ErrorCode.InvalidState, batch.Rejected[0].Error.Code);
    }

    [Fact]
    public void Run_SameDateTies_ProcessedByReference()
    {
        var entries = new[]
        {
            Samples.Cancellation("B-2", "2026-10-15"),
            Samples.Cancellation("B-1", "2026-10-15")
        };

        var batch = CancellationBatchProcessor.Run(Ledger(), entries, RefundPolicies.Flexible);

        Assert.Equal(new[] { "B-1", "B-2" }, batch.Receipts.Select(r => r.Reference));
    }

    [Fact]
    public void Run_EmptyEntries_ReturnsUnchangedLedger()
    {
        var batch = CancellationBatchProcessor.Run(Ledger(), Array.Empty<CancellationEntry>(), RefundPolicies.Flexible);

        Assert.Empty(batch.Receipts);
        Assert.Empty(batch.Rejected);
        Assert.Equal(2, batch.Ledger.ActiveBookings.Count);
    }

    [Fact]
    public void Run_DoesNotChangeInputLedger()
    {
        var ledger = Ledger();

        _ = CancellationBatchProcessor.Run(ledger, new[] { Samples.Cancellation("B-1", "2026-10-03") }, RefundPolicies.Flexible);

        Assert.Equal(2, ledger.ActiveBookings.Count);
    }
}
