using Xunit;
using TicketBookingSystem.Domain;

public class BookingTests
{
    [Fact]
    public void Factory_Success_ReturnsValid()
    {
        var request = new BookingRequest("Conference", 3, 100.0m, 50);
        var result = ValidatedBooking.Create(request);

        Assert.IsType<ValidationResult.Valid>(result);
    }

    [Fact]
    public void Factory_Rejection_ZeroQuantity()
    {
        var request = new BookingRequest("Conference", 0, 100.0m, 50);
        var result = ValidatedBooking.Create(request);

        var invalid = Assert.IsType<ValidationResult.Invalid>(result);
        Assert.Equal("Quantity must be greater than zero.", invalid.Reason);
    }

    [Fact]
    public void Factory_Rejection_ExceedsCapacity()
    {
        var request = new BookingRequest("Conference", 60, 100.0m, 50);
        var result = ValidatedBooking.Create(request);

        var invalid = Assert.IsType<ValidationResult.Invalid>(result);
        Assert.Equal("Requested quantity exceeds available capacity.", invalid.Reason);
    }

    [Fact]
    public void Distinct_Cases_PatternMatching()
    {
        var request = new BookingRequest("Test", 1, 10.0m, 10);
        ValidationResult valid = ValidatedBooking.Create(request);
        string description = valid switch
        {
            ValidationResult.Valid => "Success",
            ValidationResult.Invalid => "Failure",
            _ => "Unknown"
        };

        Assert.Equal("Success", description);
    }

    [Fact]
    public void Normal_Operation_TransitionsToConfirmed()
    {
        var request = new BookingRequest("Workshop", 2, 75.0m, 20);
        var result = ValidatedBooking.Create(request);
        var valid = (ValidationResult.Valid)result;
        BookingState state = new BookingState.Pending(valid.Booking);

        var newState = BookingWorkflow.Process(result, state);

        Assert.IsType<BookingState.Confirmed>(newState);
    }

    [Fact]
    public void Missing_Prerequisite_RejectsInvalidSequence()
    {
        var request = new BookingRequest("Workshop", -1, 75.0m, 20);
        var result = ValidatedBooking.Create(request);
        BookingState state = new BookingState.Pending(null!);

        var newState = BookingWorkflow.Process(result, state);

        var cancelled = Assert.IsType<BookingState.Cancelled>(newState);
        Assert.Equal("Quantity must be greater than zero.", cancelled.Reason);
    }

    [Fact]
    public void Repeated_Operation_MaintainsConfirmedState()
    {
        var request = new BookingRequest("Workshop", 2, 75.0m, 20);
        var result = ValidatedBooking.Create(request);
        var valid = (ValidationResult.Valid)result;
        BookingState state = new BookingState.Confirmed(valid.Booking, System.DateTime.UtcNow);

        var newState = BookingWorkflow.Process(result, state);

        Assert.IsType<BookingState.Confirmed>(newState);
    }

    [Fact]
    public void State_Preservation_FailedConstructionLeavesOriginalIntact()
    {
        var request = new BookingRequest("", 5, 50.0m, 10);
        var result = ValidatedBooking.Create(request);

        Assert.IsType<ValidationResult.Invalid>(result);
    }
}