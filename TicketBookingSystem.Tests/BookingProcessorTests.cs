using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class BookingProcessorTests
{
    [Fact]
    public void Process_ValidEntry_ReturnsProcessedBookingWithFee()
    {
        var fees = new StubFeeCalculator(123);
        var processor = new BookingProcessor(Support.CreateService(), fees);

        var processed = Support.ExpectSuccess(processor.Process(Support.Entry("B-1", "Tech Conference", 2, "WELCOME10")));

        Assert.Equal("B-1", processed.Entry.Reference);
        Assert.Equal(270m, processed.Receipt.FinalAmount);
        Assert.Equal(123L, processed.ServiceFeeCents);
        Assert.Equal(1, fees.Calls);
    }

    [Fact]
    public void Process_DomainFailure_SkipsFeeCalculation()
    {
        var fees = new StubFeeCalculator(123);
        var processor = new BookingProcessor(Support.CreateService(), fees);

        var error = Support.ExpectFailure(processor.Process(Support.Entry("B-1", "Unknown Event", 2)));

        Assert.Equal(ErrorCode.EventNotFound, error.Code);
        Assert.Equal(0, fees.Calls);
    }

    [Fact]
    public void Process_FeeFailure_PropagatesFailure()
    {
        var processor = new BookingProcessor(Support.CreateService(), new FailingFeeCalculator());

        var error = Support.ExpectFailure(processor.Process(Support.Entry("B-1", "Jazz Night", 1)));

        Assert.Equal(ErrorCode.NativeLibraryUnavailable, error.Code);
    }

    [Fact]
    public void Run_SeparatesAcceptedAndRejected()
    {
        var processor = new BookingProcessor(Support.CreateService(), new StubFeeCalculator(100));
        var entries = new[]
        {
            Support.Entry("B-1", "Tech Conference", 2),
            Support.Entry("B-2", "Unknown Event", 1),
            Support.Entry("B-3", "Jazz Night", 0),
            Support.Entry("B-4", "Jazz Night", 1)
        };

        var batch = Support.ExpectSuccess(BookingBatchProcessor.Run(entries, processor.Process));

        Assert.Equal(new[] { "B-1", "B-4" }, batch.Accepted.Select(a => a.Entry.Reference));
        Assert.Equal(new[] { "B-2", "B-3" }, batch.Rejected.Select(r => r.Entry.Reference));
        Assert.Equal(new[] { ErrorCode.EventNotFound, ErrorCode.InvalidQuantity }, batch.Rejected.Select(r => r.Error.Code));
    }

    [Fact]
    public void Run_OperationalFailure_AbortsBatch()
    {
        var processor = new BookingProcessor(Support.CreateService(), new FailingFeeCalculator());
        var entries = new[]
        {
            Support.Entry("B-1", "Unknown Event", 1),
            Support.Entry("B-2", "Jazz Night", 1)
        };

        var error = Support.ExpectFailure(BookingBatchProcessor.Run(entries, processor.Process));

        Assert.Equal(ErrorCode.NativeLibraryUnavailable, error.Code);
        Assert.Equal(ErrorCategory.Operational, error.Category);
    }

    [Fact]
    public void Run_EmptyInput_ReturnsEmptyBatch()
    {
        var processor = new BookingProcessor(Support.CreateService(), new StubFeeCalculator(100));

        var batch = Support.ExpectSuccess(BookingBatchProcessor.Run(Array.Empty<BookingEntry>(), processor.Process));

        Assert.Empty(batch.Accepted);
        Assert.Empty(batch.Rejected);
    }
}