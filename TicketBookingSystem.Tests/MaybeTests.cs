using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class MaybeTests
{
    private static EventCatalog CreateCatalog() =>
        new(new[]
        {
            new EventInfo("Tech Conference", 150m, 50),
            new EventInfo("Sold Out Gala", 300m, 0)
        });

    [Fact]
    public void Lookup_Present_ReturnsSome()
    {
        var found = Assert.IsType<Maybe<EventInfo>.Some>(CreateCatalog().Find("tech conference"));

        Assert.Equal(50, found.Value.AvailableCapacity);
    }

    [Fact]
    public void Lookup_Absent_ReturnsNone()
    {
        Assert.IsType<Maybe<EventInfo>.None>(CreateCatalog().Find("Unknown Event"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Lookup_NullOrBlankName_ReturnsNone(string? name)
    {
        Assert.IsType<Maybe<EventInfo>.None>(CreateCatalog().Find(name));
    }

    [Fact]
    public void Lookup_ZeroCapacityEvent_IsPresentNotAbsent()
    {
        var found = Assert.IsType<Maybe<EventInfo>.Some>(CreateCatalog().Find("Sold Out Gala"));

        Assert.Equal(0, found.Value.AvailableCapacity);
    }

    [Fact]
    public void Primitive_Values_AreNotAbsence()
    {
        Assert.Equal(0, Assert.IsType<Maybe<int>.Some>(Maybe.Some(0)).Value);
        Assert.False(Assert.IsType<Maybe<bool>.Some>(Maybe.Some(false)).Value);
        Assert.Equal(string.Empty, Assert.IsType<Maybe<string>.Some>(Maybe.Some(string.Empty)).Value);
    }

    [Fact]
    public void PromoLookup_ZeroDiscount_IsPresent()
    {
        var promos = new PromoCatalog(new[] { new PromoCode("NODISCOUNT", 0) });

        var found = Assert.IsType<Maybe<int>.Some>(promos.Find("NODISCOUNT"));

        Assert.Equal(0, found.Value);
    }

    [Fact]
    public void ToResult_Some_ReturnsSuccessWithSameValue()
    {
        var result = Maybe.Some(0).ToResult(() => AppError.PromoCodeNotFound("X"));

        var success = Assert.IsType<Result<int, AppError>.Success>(result);
        Assert.Equal(0, success.Value);
    }

    [Fact]
    public void ToResult_None_ReturnsSuppliedFailure()
    {
        var result = Maybe.None<int>().ToResult(() => AppError.PromoCodeNotFound("X"));

        var failure = Assert.IsType<Result<int, AppError>.Failure>(result);
        Assert.Equal(ErrorCode.PromoCodeNotFound, failure.Error.Code);
        Assert.Equal("X", failure.Error.Subject);
    }

    [Fact]
    public void Some_WithNull_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Maybe.Some<string>(null!));
    }
}