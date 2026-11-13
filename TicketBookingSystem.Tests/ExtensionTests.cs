using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class ExtensionTests
{
    [Fact]
    public void SplitResults_MixedResults_SeparatesAndPreservesOrder()
    {
        var results = new[]
        {
            Result<int, string>.Ok(1),
            Result<int, string>.Fail("first"),
            Result<int, string>.Ok(2),
            Result<int, string>.Fail("second")
        };

        var (successes, failures) = results.SplitResults();

        Assert.Equal(new[] { 1, 2 }, successes);
        Assert.Equal(new[] { "first", "second" }, failures);
    }

    [Fact]
    public void SplitResults_EmptyInput_ReturnsEmptyLists()
    {
        var (successes, failures) = Array.Empty<Result<int, string>>().SplitResults();

        Assert.Empty(successes);
        Assert.Empty(failures);
    }

    [Fact]
    public void MapError_Failure_TransformsErrorOnly()
    {
        var mapped = Result<int, string>.Fail("bad").MapError(error => error.Length);

        var failure = Assert.IsType<Result<int, int>.Failure>(mapped);
        Assert.Equal(3, failure.Error);
    }

    [Fact]
    public void MapError_Success_DoesNotInvokeMapper()
    {
        var calls = 0;

        var mapped = Result<int, string>.Ok(7).MapError(error =>
        {
            calls++;
            return error.Length;
        });

        Assert.Equal(0, calls);
        var success = Assert.IsType<Result<int, int>.Success>(mapped);
        Assert.Equal(7, success.Value);
    }

    [Fact]
    public void InPeriod_FiltersWithInclusiveBounds()
    {
        var bookings = new[]
        {
            Support.Processed("B-1", "Event", 1, 10m, 1, "2026-09-30"),
            Support.Processed("B-2", "Event", 1, 10m, 1, "2026-10-01"),
            Support.Processed("B-3", "Event", 1, 10m, 1, "2026-10-14"),
            Support.Processed("B-4", "Event", 1, 10m, 1, "2026-10-15")
        };

        var result = bookings.InPeriod(Support.Date("2026-10-01"), Support.Date("2026-10-14"));

        Assert.Equal(new[] { "B-2", "B-3" }, result.Select(b => b.Entry.Reference));
    }

    [Fact]
    public void InPeriod_NoMatches_ReturnsEmpty()
    {
        var bookings = new[] { Support.Processed("B-1", "Event", 1, 10m, 1, "2026-01-01") };

        var result = bookings.InPeriod(Support.Date("2026-10-01"), Support.Date("2026-10-14"));

        Assert.Empty(result);
    }

    [Fact]
    public void InPeriod_IsDeferredAndReadOnly()
    {
        var source = new List<ProcessedBooking> { Support.Processed("B-1", "Event", 1, 10m, 1, "2026-10-02") };
        var query = source.InPeriod(Support.Date("2026-10-01"), Support.Date("2026-10-14"));

        source.Add(Support.Processed("B-2", "Event", 1, 10m, 1, "2026-10-03"));

        Assert.Equal(2, query.Count());
        Assert.Equal(2, source.Count);
    }
}