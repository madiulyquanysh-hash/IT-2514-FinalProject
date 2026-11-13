using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class BookingServiceTests
{
    private static BookingService CreateService() =>
        new(
            new EventCatalog(new[]
            {
                new EventInfo("Tech Conference", 150m, 50),
                new EventInfo("Jazz Night", 45.5m, 10),
                new EventInfo("Sold Out Gala", 300m, 0)
            }),
            new PromoCatalog(new[]
            {
                new PromoCode("WELCOME10", 10),
                new PromoCode("NODISCOUNT", 0)
            }));

    private static T ExpectSuccess<T>(Result<T, AppError> result) =>
        Assert.IsType<Result<T, AppError>.Success>(result).Value;

    private static AppError ExpectFailure<T>(Result<T, AppError> result) =>
        Assert.IsType<Result<T, AppError>.Failure>(result).Error;

    [Fact]
    public void Book_NormalPath_ReturnsReceiptWithoutDiscount()
    {
        var receipt = ExpectSuccess(CreateService().Book(new BookingCommand("Tech Conference", 2)));

        Assert.Equal("Tech Conference", receipt.EventName);
        Assert.Equal(2, receipt.Quantity);
        Assert.Equal(0, receipt.DiscountPercent);
        Assert.Equal(300m, receipt.FinalAmount);
    }

    [Fact]
    public void Book_WithPromo_AppliesDiscount()
    {
        var receipt = ExpectSuccess(CreateService().Book(new BookingCommand("Tech Conference", 2, "WELCOME10")));

        Assert.Equal(10, receipt.DiscountPercent);
        Assert.Equal(270m, receipt.FinalAmount);
    }

    [Fact]
    public void Book_WithZeroPercentPromo_KeepsFullPrice()
    {
        var receipt = ExpectSuccess(CreateService().Book(new BookingCommand("Jazz Night", 2, "NODISCOUNT")));

        Assert.Equal(0, receipt.DiscountPercent);
        Assert.Equal(91m, receipt.FinalAmount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Book_BlankPromo_IsTreatedAsNoPromo(string? promoCode)
    {
        var receipt = ExpectSuccess(CreateService().Book(new BookingCommand("Jazz Night", 1, promoCode)));

        Assert.Equal(0, receipt.DiscountPercent);
        Assert.Equal(45.5m, receipt.FinalAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Book_InvalidQuantity_ReturnsInvalidQuantityError(int quantity)
    {
        var error = ExpectFailure(CreateService().Book(new BookingCommand("Tech Conference", quantity)));

        Assert.Equal(ErrorCode.InvalidQuantity, error.Code);
        Assert.Equal(ErrorCategory.Domain, error.Category);
        Assert.Equal("Quantity must be greater than zero.", error.Message);
        Assert.Equal("Tech Conference", error.Subject);
    }

    [Fact]
    public void Book_ExceedsCapacity_ReturnsCapacityExceededError()
    {
        var error = ExpectFailure(CreateService().Book(new BookingCommand("Jazz Night", 11)));

        Assert.Equal(ErrorCode.CapacityExceeded, error.Code);
        Assert.Equal("Jazz Night", error.Subject);
    }

    [Fact]
    public void Book_SoldOutEvent_ReturnsCapacityExceededNotNotFound()
    {
        var error = ExpectFailure(CreateService().Book(new BookingCommand("Sold Out Gala", 1)));

        Assert.Equal(ErrorCode.CapacityExceeded, error.Code);
    }

    [Fact]
    public void Book_MissingEvent_ReturnsEventNotFound()
    {
        var error = ExpectFailure(CreateService().Book(new BookingCommand("Unknown Event", 1)));

        Assert.Equal(ErrorCode.EventNotFound, error.Code);
        Assert.Equal("Unknown Event", error.Subject);
    }

    [Fact]
    public void Book_UnknownPromo_ReturnsPromoCodeNotFound()
    {
        var error = ExpectFailure(CreateService().Book(new BookingCommand("Jazz Night", 1, "BOGUS")));

        Assert.Equal(ErrorCode.PromoCodeNotFound, error.Code);
        Assert.Equal("BOGUS", error.Subject);
    }

    [Fact]
    public void Confirm_AlreadyConfirmed_ReturnsInvalidState()
    {
        var service = CreateService();
        var priced = ExpectSuccess(service.Prepare(new BookingCommand("Jazz Night", 1)));

        var error = ExpectFailure(service.Confirm(priced, new BookingState.Confirmed(priced.Booking, DateTime.UtcNow)));

        Assert.Equal(ErrorCode.InvalidState, error.Code);
        Assert.Equal("Confirmed", error.Subject);
    }

    [Fact]
    public void Confirm_Cancelled_ReturnsInvalidState()
    {
        var service = CreateService();
        var priced = ExpectSuccess(service.Prepare(new BookingCommand("Jazz Night", 1)));

        var error = ExpectFailure(service.Confirm(priced, new BookingState.Cancelled("Customer cancelled")));

        Assert.Equal(ErrorCode.InvalidState, error.Code);
        Assert.Equal("Cancelled", error.Subject);
    }

    [Fact]
    public void Confirm_Pending_ReturnsReceipt()
    {
        var service = CreateService();
        var priced = ExpectSuccess(service.Prepare(new BookingCommand("Jazz Night", 2)));

        var receipt = ExpectSuccess(service.Confirm(priced, new BookingState.Pending(priced.Booking)));

        Assert.Equal(91m, receipt.FinalAmount);
    }
}