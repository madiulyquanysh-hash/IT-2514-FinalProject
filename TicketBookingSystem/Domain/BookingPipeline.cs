namespace TicketBookingSystem.Domain;

public sealed record PipelineSettings(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal MinimumAmount,
    Func<ProcessedBooking, long> Score,
    Func<int, int> RefundPolicy);

public sealed record PipelineInput(
    EventCatalog Events,
    PromoCatalog Promos,
    IReadOnlyList<BookingEntry> Bookings,
    IReadOnlyList<CancellationEntry> Cancellations,
    IFeeCalculator Fees,
    PipelineSettings Settings);

public sealed record ProcessedStage(PipelineInput Input, BookingBatch Batch);

public sealed record AllocatedStage(ProcessedStage Previous, AllocationResult Allocation);

public sealed record CancelledStage(AllocatedStage Previous, CancellationBatch Cancellations, SeatInventory Inventory);

public sealed record PipelineResult(
    PipelineInput Input,
    BookingBatch Batch,
    AllocationResult Allocation,
    CancellationBatch Cancellations,
    SeatInventory Inventory,
    IReadOnlyList<EventRevenueRow> Revenue,
    HighValueReport HighValue);

public static class BookingPipeline
{
    public static Result<PipelineResult, AppError> Run(PipelineInput input) =>
        ProcessBookings(input)
            .Map(stage => AllocateSeats(stage))
            .Bind(stage => ApplyCancellations(stage))
            .Bind(stage => BuildReports(stage));

    public static Result<ProcessedStage, AppError> ProcessBookings(PipelineInput input)
    {
        var processor = new BookingProcessor(new BookingService(input.Events, input.Promos), input.Fees);

        return BookingBatchProcessor.Run(input.Bookings, processor.Process)
            .Map(batch => new ProcessedStage(input, batch));
    }

    public static AllocatedStage AllocateSeats(ProcessedStage stage) =>
        new(stage, SeatAllocator.Allocate(stage.Batch.Accepted, SeatInventory.FromCatalog(stage.Input.Events)));

    public static Result<CancelledStage, AppError> ApplyCancellations(AllocatedStage stage)
    {
        var input = stage.Previous.Input;
        var ledger = BookingLedger.From(stage.Allocation.Allocated);
        var batch = CancellationBatchProcessor.Run(ledger, input.Cancellations, input.Settings.RefundPolicy);

        return ReleaseSeats(stage.Allocation.Inventory, batch.Receipts)
            .Map(inventory => new CancelledStage(stage, batch, inventory));
    }

    public static Result<PipelineResult, AppError> BuildReports(CancelledStage stage)
    {
        var processed = stage.Previous.Previous;
        var settings = processed.Input.Settings;
        var active = stage.Cancellations.Ledger.ActiveBookings;

        return BookingReports
            .HighValue(active, settings.PeriodStart, settings.PeriodEnd, settings.MinimumAmount, settings.Score)
            .Map(highValue => new PipelineResult(
                processed.Input,
                processed.Batch,
                stage.Previous.Allocation,
                stage.Cancellations,
                stage.Inventory,
                BookingReports.EventRevenue(active),
                highValue));
    }

    private static Result<SeatInventory, AppError> ReleaseSeats(
        SeatInventory inventory,
        IEnumerable<CancellationReceipt> receipts) =>
        receipts.Aggregate(
            Result<SeatInventory, AppError>.Ok(inventory),
            (current, receipt) => current.Bind(next => next.Release(receipt.EventName, receipt.Quantity)));
}
