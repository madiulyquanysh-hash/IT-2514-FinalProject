namespace TicketBookingSystem.Domain;

public sealed class BookingProcessor
{
    private readonly BookingService service;
    private readonly IFeeCalculator fees;

    public BookingProcessor(BookingService service, IFeeCalculator fees)
    {
        this.service = service;
        this.fees = fees;
    }

    public Result<ProcessedBooking, AppError> Process(BookingEntry entry) =>
        service.Book(new BookingCommand(entry.EventName, entry.Quantity, entry.PromoCode))
            .Bind(receipt => fees.CalculateFeeCents(receipt.FinalAmount, receipt.Quantity)
                .Map(fee => new ProcessedBooking(entry, receipt, fee)));
}

public static class BookingBatchProcessor
{
    public static IEnumerable<Result<ProcessedBooking, RejectedBooking>> Stream(
        IEnumerable<BookingEntry> entries,
        Func<BookingEntry, Result<ProcessedBooking, AppError>> process)
    {
        foreach (var entry in entries)
        {
            yield return process(entry).MapError(error => new RejectedBooking(entry, error));
        }
    }

    public static Result<BookingBatch, AppError> Run(
        IEnumerable<BookingEntry> entries,
        Func<BookingEntry, Result<ProcessedBooking, AppError>> process)
    {
        var results = Stream(entries, process).ToArray();
        var (accepted, rejected) = results.SplitResults();
        var operational = rejected.FirstOrDefault(item => item.Error.Category == ErrorCategory.Operational);

        return operational is null
            ? Result<BookingBatch, AppError>.Ok(new BookingBatch(accepted, rejected))
            : Result<BookingBatch, AppError>.Fail(operational.Error);
    }
}