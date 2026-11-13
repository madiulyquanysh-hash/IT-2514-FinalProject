using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class ScoringTests
{
    private static readonly ProcessedBooking B001 =
        Support.Processed("B-001", "Tech Conference", 2, 270m, 775, "2026-10-01", discount: 10);

    private static readonly ProcessedBooking B003 =
        Support.Processed("B-003", "Tech Conference", 5, 750m, 2125, "2026-10-05");

    private static readonly ProcessedBooking B004 =
        Support.Processed("B-004", "Jazz Night", 10, 455m, 1638, "2026-10-07");

    [Fact]
    public void CreateScore_RevenueFocused_ProducesExpectedScores()
    {
        var score = Scoring.CreateScore(new ScoreConfig(1, 10, 2));

        Assert.Equal(75L, score(B003));
        Assert.Equal(50L, score(B004));
        Assert.Equal(2L, score(B001));
    }

    [Fact]
    public void CreateScore_VolumeFocused_ProducesExpectedScores()
    {
        var score = Scoring.CreateScore(new ScoreConfig(10, 1, 0));

        Assert.Equal(57L, score(B003));
        Assert.Equal(104L, score(B004));
        Assert.Equal(22L, score(B001));
    }

    [Fact]
    public void PartialApplications_SameInput_ProduceDifferentResults()
    {
        var revenueFocused = Scoring.CreateScore(new ScoreConfig(1, 10, 2));
        var volumeFocused = Scoring.CreateScore(new ScoreConfig(10, 1, 0));
        var dataset = new[] { B001, B003, B004 };

        Assert.NotEqual(revenueFocused(B003), volumeFocused(B003));

        var revenueOrder = dataset.OrderByDescending(revenueFocused).Select(b => b.Entry.Reference);
        var volumeOrder = dataset.OrderByDescending(volumeFocused).Select(b => b.Entry.Reference);

        Assert.Equal(new[] { "B-003", "B-004", "B-001" }, revenueOrder);
        Assert.Equal(new[] { "B-004", "B-003", "B-001" }, volumeOrder);
    }

    [Fact]
    public void PartialApplications_AreIndependentOfEachOther()
    {
        var first = Scoring.CreateScore(new ScoreConfig(1, 10, 2));
        var before = first(B003);

        var second = Scoring.CreateScore(new ScoreConfig(100, 100, 0));
        _ = second(B003);

        Assert.Equal(before, first(B003));
    }
}