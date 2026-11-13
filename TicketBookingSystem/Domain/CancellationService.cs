using System.Collections.Immutable;
using System.Globalization;

namespace TicketBookingSystem.Domain;

public sealed record CancellationOutcome(BookingLedger Ledger, CancellationReceipt Receipt);

public sealed record CancellationBatch(
    BookingLedger Ledger,
    IReadOnlyList<CancellationReceipt> Receipts,
    IReadOnlyList<RejectedCancellation> Rejected);

public static class RefundPolicies
{
    public static readonly Func<int, int> Flexible = days => days switch
    {
        < 0 => 0,
        <= 7 => 100,
        <= 14 => 50,
        _ => 0
    };

    public static readonly Func<int, int> Strict = days => days switch
    {
        < 0 => 0,
        <= 2 => 100,
        _ => 0
    };
}

public static class CancellationService
{
    public static Result<CancellationOutcome, AppError> Cancel(
        BookingLedger ledger,
        CancellationEntry request,
        Func<int, int> refundPolicy) =>
        FindBooking(ledger, request)
            .Bind(entry => EnsureActive(entry))
            .Bind(entry => ElapsedDays(entry, request))
            .Bind(elapsed => RefundPercentFor(elapsed, refundPolicy))
            .Map(priced => Settle(ledger, request, priced));

    private static Result<LedgerEntry, AppError> FindBooking(BookingLedger ledger, CancellationEntry request) =>
        ledger.Find(request.Reference)
            .ToResult(() => AppError.BookingNotFound(request.Reference));

    private static Result<LedgerEntry, AppError> EnsureActive(LedgerEntry entry) =>
        entry.Status switch
        {
            BookingStatus.Active =>
                Result<LedgerEntry, AppError>.Ok(entry),
            BookingStatus.Cancelled cancelled =>
                Result<LedgerEntry, AppError>.Fail(AppError.InvalidState(
                    $"Booking was already cancelled on {cancelled.On.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.",
                    "Cancelled")),
            _ =>
                Result<LedgerEntry, AppError>.Fail(AppError.InvalidState("Booking has an unknown status.", "Unknown"))
        };

    private static Result<Elapsed, AppError> ElapsedDays(LedgerEntry entry, CancellationEntry request)
    {
        var days = request.CancelledOn.DayNumber - entry.Booking.Entry.BookedOn.DayNumber;

        return days < 0
            ? Result<Elapsed, AppError>.Fail(AppError.InvalidCancellationDate(request.Reference))
            : Result<Elapsed, AppError>.Ok(new Elapsed(entry, days));
    }

    private static Result<Priced, AppError> RefundPercentFor(Elapsed elapsed, Func<int, int> refundPolicy)
    {
        var percent = refundPolicy(elapsed.Days);

        return percent is < 0 or > 100
            ? Result<Priced, AppError>.Fail(AppError.InvalidRefundPercent(percent))
            : Result<Priced, AppError>.Ok(new Priced(elapsed.Entry, elapsed.Days, percent));
    }

    private static CancellationOutcome Settle(BookingLedger ledger, CancellationEntry request, Priced priced)
    {
        var booking = priced.Entry.Booking;
        var refund = decimal.Round(booking.Receipt.FinalAmount * priced.Percent / 100m, 2, MidpointRounding.AwayFromZero);

        var receipt = new CancellationReceipt(
            booking.Entry.Reference,
            booking.Entry.Customer,
            booking.Receipt.EventName,
            booking.Receipt.Quantity,
            priced.Days,
            priced.Percent,
            refund,
            booking.Receipt.FinalAmount - refund,
            request.Reason);

        var cancelled = priced.Entry with { Status = new BookingStatus.Cancelled(request.CancelledOn, request.Reason) };
        return new CancellationOutcome(ledger.With(cancelled), receipt);
    }

    private sealed record Elapsed(LedgerEntry Entry, int Days);

    private sealed record Priced(LedgerEntry Entry, int Days, int Percent);
}

public static class CancellationBatchProcessor
{
    public static CancellationBatch Run(
        BookingLedger ledger,
        IEnumerable<CancellationEntry> entries,
        Func<int, int> refundPolicy)
    {
        var ordered = entries
            .OrderBy(entry => entry.CancelledOn)
            .ThenBy(entry => entry.Reference, StringComparer.Ordinal);

        var seed = new BatchState(
            ledger,
            ImmutableList<CancellationReceipt>.Empty,
            ImmutableList<RejectedCancellation>.Empty);

        var final = ordered.Aggregate(seed, (state, entry) => Step(state, entry, refundPolicy));
        return new CancellationBatch(final.Ledger, final.Receipts, final.Rejected);
    }

    private static BatchState Step(BatchState state, CancellationEntry entry, Func<int, int> refundPolicy) =>
        CancellationService.Cancel(state.Ledger, entry, refundPolicy).Match(
            outcome => state with { Ledger = outcome.Ledger, Receipts = state.Receipts.Add(outcome.Receipt) },
            error => state with { Rejected = state.Rejected.Add(new RejectedCancellation(entry, error)) });

    private sealed record BatchState(
        BookingLedger Ledger,
        ImmutableList<CancellationReceipt> Receipts,
        ImmutableList<RejectedCancellation> Rejected);
}
