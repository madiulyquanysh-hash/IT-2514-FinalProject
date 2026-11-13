using TicketBookingSystem.Domain;
using TicketBookingSystem.Infrastructure;
using Xunit;

namespace TicketBookingSystem.Tests;

public sealed class ResourceLifetimeTests : TempDirectoryTestBase
{
    private sealed class TrackingReader : StringReader
    {
        public TrackingReader(string text) : base(text)
        {
        }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingReader : TextReader
    {
        private int reads;

        public bool Disposed { get; private set; }

        public override string? ReadLine()
        {
            reads++;

            if (reads == 1)
            {
                return "first";
            }

            throw new IOException("simulated read failure");
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private static void AssertFileReleased(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(stream.CanWrite);
    }

    [Fact]
    public void LineSource_NotOpenedBeforeEnumeration()
    {
        var opened = 0;
        var reader = new TrackingReader("a\nb");

        var lines = LineSource.ReadLines(() =>
        {
            opened++;
            return reader;
        });

        Assert.Equal(0, opened);
        using var enumerator = lines.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, opened);
    }

    [Fact]
    public void LineSource_FullEnumeration_DisposesReader()
    {
        var reader = new TrackingReader("a\nb\nc");

        var lines = LineSource.ReadLines(() => reader).ToArray();

        Assert.Equal(new[] { "a", "b", "c" }, lines);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void LineSource_EarlyStop_DisposesReader()
    {
        var reader = new TrackingReader("a\nb\nc");

        var first = LineSource.ReadLines(() => reader).First();

        Assert.Equal("a", first);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void LineSource_ReaderFailure_DisposesReader()
    {
        var reader = new ThrowingReader();

        Assert.Throws<IOException>(() => LineSource.ReadLines(() => reader).ToArray());

        Assert.True(reader.Disposed);
    }

    [Fact]
    public void Export_Success_WritesContentAndReleasesFile()
    {
        var path = PathOf("report.txt");
        var lines = new[] { "first", "second" };

        var exported = Support.ExpectSuccess(ReportExporter.Export(path, lines));

        Assert.Equal(path, exported);
        Assert.Equal(lines, File.ReadAllLines(path));
        Assert.False(File.Exists(path + ".tmp"));
        AssertFileReleased(path);
    }

    [Fact]
    public void Export_CreatesMissingDirectory()
    {
        var path = Path.Combine(Folder, "nested", "deeper", "report.txt");

        _ = Support.ExpectSuccess(ReportExporter.Export(path, new[] { "line" }));

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Export_Failure_ReturnsErrorAndPreservesExistingFile()
    {
        var path = WriteFile("report.txt", "old content");
        Directory.CreateDirectory(path + ".tmp");

        var error = Support.ExpectFailure(ReportExporter.Export(path, new[] { "new content" }));

        Assert.Equal(ErrorCode.FileAccessError, error.Code);
        Assert.Equal(ErrorCategory.Operational, error.Category);
        Assert.Equal(path, error.Subject);
        Assert.Equal("old content", File.ReadAllText(path));
    }

    [Fact]
    public void Loader_Success_ReleasesFile()
    {
        var path = WriteFile("bookings.csv", Support.BookingsHeader + Environment.NewLine + "B-1,Alice,Jazz Night,1,,2026-10-01");

        _ = Support.ExpectSuccess(BookingFileLoader.Load(path));

        AssertFileReleased(path);
    }

    [Fact]
    public void Loader_MalformedEarlyStop_ReleasesFile()
    {
        var path = WriteFile(
            "bookings.csv",
            Support.BookingsHeader + Environment.NewLine + "B-1,Alice,Jazz Night,two,,2026-10-01" + Environment.NewLine + "B-2,Bob,Jazz Night,1,,2026-10-02");

        var error = Support.ExpectFailure(BookingFileLoader.Load(path));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        AssertFileReleased(path);
    }
}