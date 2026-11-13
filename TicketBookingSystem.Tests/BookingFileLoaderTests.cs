using TicketBookingSystem.Domain;
using TicketBookingSystem.Infrastructure;
using Xunit;

namespace TicketBookingSystem.Tests;

public sealed class BookingFileLoaderTests : TempDirectoryTestBase
{
    private static string WithHeader(string dataLine) =>
        Support.BookingsHeader + Environment.NewLine + dataLine;

    [Fact]
    public void Load_ValidFile_ReturnsEntries()
    {
        var content = string.Join(
            Environment.NewLine,
            Support.BookingsHeader,
            "B-1,Alice,Tech Conference,2,WELCOME10,2026-10-01",
            string.Empty,
            "B-2,Bob,Jazz Night,1,,2026-10-02");
        var path = WriteFile("bookings.csv", content);

        var entries = Support.ExpectSuccess(BookingFileLoader.Load(path));

        Assert.Equal(2, entries.Count);
        Assert.Equal("B-1", entries[0].Reference);
        Assert.Equal("WELCOME10", entries[0].PromoCode);
        Assert.Equal(Support.Date("2026-10-01"), entries[0].BookedOn);
        Assert.Null(entries[1].PromoCode);
        Assert.Equal(1, entries[1].Quantity);
    }

    [Fact]
    public void Load_MissingFile_ReturnsFileNotFound()
    {
        var path = PathOf("missing.csv");

        var error = Support.ExpectFailure(BookingFileLoader.Load(path));

        Assert.Equal(ErrorCode.FileNotFound, error.Code);
        Assert.Equal(ErrorCategory.Operational, error.Category);
        Assert.Equal(path, error.Subject);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsMalformedContent()
    {
        var path = WriteFile("empty.csv", string.Empty);

        var error = Support.ExpectFailure(BookingFileLoader.Load(path));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Equal(path, error.Subject);
    }

    [Fact]
    public void Load_WrongHeader_ReturnsMalformedContent()
    {
        var path = WriteFile("header.csv", "reference,customer" + Environment.NewLine + "B-1,Alice");

        var error = Support.ExpectFailure(BookingFileLoader.Load(path));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Contains("Line 1", error.Message);
    }

    [Theory]
    [InlineData("B-1,Alice,Tech Conference")]
    [InlineData("B-1,Alice,Tech Conference,2,,2026-10-01,extra")]
    [InlineData("B-1,Alice,Tech Conference,two,,2026-10-01")]
    [InlineData("B-1,Alice,Tech Conference,2.5,,2026-10-01")]
    [InlineData("B-1,Alice,Tech Conference,2,,01/10/2026")]
    [InlineData("B-1,Alice,Tech Conference,2,,2026-13-45")]
    [InlineData(",Alice,Tech Conference,2,,2026-10-01")]
    [InlineData("B-1,,Tech Conference,2,,2026-10-01")]
    public void Load_MalformedDataLine_ReturnsMalformedContent(string dataLine)
    {
        var path = WriteFile("bad.csv", WithHeader(dataLine));

        var error = Support.ExpectFailure(BookingFileLoader.Load(path));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Equal(ErrorCategory.Operational, error.Category);
        Assert.Equal(path, error.Subject);
        Assert.Contains("Line 2", error.Message);
    }
}