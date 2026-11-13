using TicketBookingSystem.Domain;
using TicketBookingSystem.Infrastructure;
using Xunit;

namespace TicketBookingSystem.Tests;

public sealed class CancellationFileTests : TempDirectoryTestBase
{
    private const string Header = "reference,cancelledOn,reason";

    private static string WithHeader(string dataLine) => Header + Environment.NewLine + dataLine;

    private static void AssertFileReleased(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void CancellationStream_ValidLines_ParsesEntries()
    {
        var lines = new[] { Header, "B-1,2026-10-03,Change of plans", string.Empty, "B-2,2026-10-04," };

        var entries = CancellationStream.ParseEntries("memory", lines).Select(Support.ExpectSuccess).ToArray();

        Assert.Equal(2, entries.Length);
        Assert.Equal("B-1", entries[0].Reference);
        Assert.Equal(Support.Date("2026-10-03"), entries[0].CancelledOn);
        Assert.Equal("Change of plans", entries[0].Reason);
        Assert.Equal("unspecified", entries[1].Reason);
    }

    [Fact]
    public void BookingStream_DuplicateReference_ReportsLineNumber()
    {
        var lines = new[]
        {
            Support.BookingsHeader,
            "B-1,Alice,Tech Conference,2,,2026-10-01",
            "B-1,Bob,Jazz Night,1,,2026-10-02"
        };

        var results = BookingStream.ParseEntries("memory", lines).ToArray();

        Assert.Equal(2, results.Length);
        var error = Support.ExpectFailure(results[1]);
        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Contains("Line 3", error.Message);
        Assert.Contains("duplicated", error.Message);
    }

    [Fact]
    public void Load_ValidFile_ReturnsEntries()
    {
        var path = WriteFile("cancellations.csv", WithHeader("B-1,2026-10-03,Change of plans"));

        var entries = Support.ExpectSuccess(CancellationFileLoader.Load(path));

        Assert.Single(entries);
        Assert.Equal("B-1", entries[0].Reference);
        AssertFileReleased(path);
    }

    [Fact]
    public void Load_MissingFile_ReturnsFileNotFound()
    {
        var path = PathOf("missing.csv");

        var error = Support.ExpectFailure(CancellationFileLoader.Load(path));

        Assert.Equal(ErrorCode.FileNotFound, error.Code);
        Assert.Equal(ErrorCategory.Operational, error.Category);
        Assert.Equal(path, error.Subject);
    }

    [Theory]
    [InlineData("")]
    [InlineData("reference,reason")]
    public void Load_EmptyFileOrWrongHeader_ReturnsMalformedContent(string content)
    {
        var path = WriteFile("bad.csv", content);

        var error = Support.ExpectFailure(CancellationFileLoader.Load(path));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Equal(path, error.Subject);
    }

    [Theory]
    [InlineData("B-1,2026-10-03")]
    [InlineData("B-1,2026-10-03,reason,extra")]
    [InlineData("B-1,10/03/2026,reason")]
    [InlineData(",2026-10-03,reason")]
    public void Load_MalformedDataLine_ReturnsMalformedContent(string dataLine)
    {
        var path = WriteFile("bad.csv", WithHeader(dataLine));

        var error = Support.ExpectFailure(CancellationFileLoader.Load(path));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Equal(ErrorCategory.Operational, error.Category);
        Assert.Equal(path, error.Subject);
        Assert.Contains("Line 2", error.Message);
        AssertFileReleased(path);
    }
}
