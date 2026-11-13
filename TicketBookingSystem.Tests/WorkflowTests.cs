using TicketBookingSystem.Domain;
using TicketBookingSystem.Infrastructure;
using Xunit;

namespace TicketBookingSystem.Tests;

public sealed class WorkflowTests : TempDirectoryTestBase
{
    private const string CatalogJson = """
    {
      "events": [
        { "name": "Tech Conference", "unitPrice": 150.0, "availableCapacity": 50 },
        { "name": "Jazz Night", "unitPrice": 45.5, "availableCapacity": 10 },
        { "name": "Sold Out Gala", "unitPrice": 300.0, "availableCapacity": 0 }
      ],
      "promoCodes": [
        { "code": "WELCOME10", "discountPercent": 10 },
        { "code": "NODISCOUNT", "discountPercent": 0 }
      ]
    }
    """;

    private const string BookingsCsv = """
    reference,customer,eventName,quantity,promoCode,bookedOn
    B-001,Alice,Tech Conference,2,WELCOME10,2026-10-01
    B-002,Bob,Jazz Night,4,,2026-10-02
    B-003,Carol,Tech Conference,5,,2026-10-05
    B-004,Dave,Jazz Night,6,NODISCOUNT,2026-10-07
    B-005,Erin,Tech Conference,1,WELCOME10,2026-10-09
    B-006,Frank,Sold Out Gala,1,,2026-10-10
    B-007,Grace,Unknown Event,2,,2026-10-11
    B-008,Heidi,Jazz Night,0,,2026-10-12
    B-009,Ivan,Jazz Night,3,BOGUS,2026-10-13
    B-010,Judy,Tech Conference,3,NODISCOUNT,2026-10-15
    B-011,Kate,Jazz Night,2,,2026-10-16
    """;

    private const string CancellationsCsv = """
    reference,cancelledOn,reason
    B-001,2026-10-03,Change of plans
    B-003,2026-10-15,Schedule conflict
    B-001,2026-10-20,Duplicate request
    B-099,2026-10-04,Unknown booking
    B-005,2026-10-01,Typo in date
    """;

    private WorkflowRequest CreateRequest(
        IFeeCalculator fees,
        string? bookings = null,
        string? cancellations = null)
    {
        return new WorkflowRequest(
            WriteFile("catalog.json", CatalogJson),
            WriteFile("bookings.csv", bookings ?? BookingsCsv),
            WriteFile("cancellations.csv", cancellations ?? CancellationsCsv),
            PathOf("report.txt"),
            Samples.Settings(),
            fees);
    }

    [Fact]
    public void NormalPath_RunsAllStagesWithRealNativeCall()
    {
        var request = CreateRequest(new NativeFeeAdapter(FeeSchedule.Standard));

        var result = Support.ExpectSuccess(ReportWorkflow.Run(request)).Result;

        Assert.Equal(7, result.Batch.Accepted.Count);
        Assert.Equal(4, result.Batch.Rejected.Count);
        Assert.Equal(6, result.Allocation.Allocated.Count);
        Assert.Equal(new[] { "B-011" }, result.Allocation.Waitlisted.Select(b => b.Entry.Reference));
        Assert.Equal(new[] { "B-001", "B-003" }, result.Cancellations.Receipts.Select(r => r.Reference));
        Assert.Equal(new[] { 270m, 375m }, result.Cancellations.Receipts.Select(r => r.RefundAmount));

        Assert.Equal(new[] { "Tech Conference", "Jazz Night" }, result.Revenue.Select(r => r.EventName));
        Assert.Equal(new[] { 585m, 455m }, result.Revenue.Select(r => r.Revenue));
        Assert.Equal(new[] { 1663L, 1638L }, result.Revenue.Select(r => r.ServiceFeeCents));

        Assert.Equal(new[] { "B-010", "B-004", "B-002", "B-005" }, result.HighValue.Rows.Select(r => r.Reference));
        Assert.Equal(new[] { 1275L, 983L, 655L, 388L }, result.HighValue.Rows.Select(r => r.ServiceFeeCents));
        Assert.Equal(new[] { 43L, 26L, 14L, -9L }, result.HighValue.Rows.Select(r => r.Score));
        Assert.Equal(1040m, result.HighValue.TotalAmount);
        Assert.Equal(3301L, result.HighValue.TotalServiceFeeCents);

        Assert.Equal(
            new[] { new SeatLevel("Jazz Night", 10, 0), new SeatLevel("Sold Out Gala", 0, 0), new SeatLevel("Tech Conference", 50, 46) },
            result.Inventory.Levels);
    }

    [Fact]
    public void NormalPath_ExportedReportContainsEverySection()
    {
        var request = CreateRequest(new NativeFeeAdapter(FeeSchedule.Standard));

        var output = Support.ExpectSuccess(ReportWorkflow.Run(request));

        var lines = File.ReadAllLines(output.ExportedPath);
        Assert.Contains("Event revenue", lines);
        Assert.Contains("Tech Conference,2,4,585.00,16.63", lines);
        Assert.Contains("Jazz Night,2,10,455.00,16.38", lines);
        Assert.Contains("High-value bookings", lines);
        Assert.Contains("B-010,Judy,Tech Conference,450.00,12.75,43", lines);
        Assert.Contains("TOTAL,,,1040.00,33.01,", lines);
        Assert.Contains("Cancellations and refunds", lines);
        Assert.Contains("B-001,Alice,Tech Conference,2,2,100,270.00,0.00,Change of plans", lines);
        Assert.Contains("B-003,Carol,Tech Conference,5,10,50,375.00,375.00,Schedule conflict", lines);
        Assert.Contains("Rejected and waitlisted bookings", lines);
        Assert.Contains("B-006,Frank,Sold Out Gala,CapacityExceeded", lines);
        Assert.Contains("B-011,Kate,Jazz Night,CapacityExceeded", lines);
        Assert.Contains("Seat inventory", lines);
        Assert.Contains("Tech Conference,50,46", lines);
        Assert.Contains("Jazz Night,10,0", lines);
    }

    [Fact]
    public void NormalPath_SnapshotCanBeReusedWithAnotherConfiguredFunction()
    {
        var request = CreateRequest(new NativeFeeAdapter(FeeSchedule.Standard));
        var result = Support.ExpectSuccess(ReportWorkflow.Run(request)).Result;
        var volumeFocused = Scoring.CreateScore(new ScoreConfig(10, 1, 0));

        var volume = Support.ExpectSuccess(BookingReports.HighValue(
            result.Cancellations.Ledger.ActiveBookings,
            request.Settings.PeriodStart,
            request.Settings.PeriodEnd,
            request.Settings.MinimumAmount,
            volumeFocused));

        Assert.Equal(new[] { "B-004", "B-002", "B-010", "B-005" }, volume.Rows.Select(r => r.Reference));
        Assert.Equal(new[] { 62L, 41L, 34L, 11L }, volume.Rows.Select(r => r.Score));
    }

    [Fact]
    public void NativeUnavailable_ReportsFailureAndPreservesExistingOutput()
    {
        var request = CreateRequest(new FailingFeeCalculator());
        File.WriteAllText(request.OutputPath, "previous report");

        var error = Support.ExpectFailure(ReportWorkflow.Run(request));

        Assert.Equal(ErrorCode.NativeLibraryUnavailable, error.Code);
        Assert.Equal("previous report", File.ReadAllText(request.OutputPath));
        Assert.False(File.Exists(request.OutputPath + ".tmp"));
    }

    [Fact]
    public void MalformedBookings_ReportsFailureAndPreservesExistingOutput()
    {
        var malformed = Support.BookingsHeader + Environment.NewLine + "B-1,Alice,Jazz Night,two,,2026-10-01";
        var request = CreateRequest(new NativeFeeAdapter(FeeSchedule.Standard), malformed);
        File.WriteAllText(request.OutputPath, "previous report");

        var error = Support.ExpectFailure(ReportWorkflow.Run(request));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Equal("previous report", File.ReadAllText(request.OutputPath));
    }

    [Fact]
    public void MalformedCancellations_ReportsFailureAndPreservesExistingOutput()
    {
        var malformed = "reference,cancelledOn,reason" + Environment.NewLine + "B-001,not-a-date,reason";
        var request = CreateRequest(new NativeFeeAdapter(FeeSchedule.Standard), null, malformed);
        File.WriteAllText(request.OutputPath, "previous report");

        var error = Support.ExpectFailure(ReportWorkflow.Run(request));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Equal("previous report", File.ReadAllText(request.OutputPath));
    }

    [Fact]
    public void MissingBookingsFile_ReportsFailureAndCreatesNoOutput()
    {
        var request = CreateRequest(new NativeFeeAdapter(FeeSchedule.Standard)) with { BookingsPath = PathOf("missing.csv") };

        var error = Support.ExpectFailure(ReportWorkflow.Run(request));

        Assert.Equal(ErrorCode.FileNotFound, error.Code);
        Assert.False(File.Exists(request.OutputPath));
    }

    [Fact]
    public void MissingCancellationsFile_ReportsFailureAndCreatesNoOutput()
    {
        var request = CreateRequest(new NativeFeeAdapter(FeeSchedule.Standard)) with { CancellationsPath = PathOf("missing.csv") };

        var error = Support.ExpectFailure(ReportWorkflow.Run(request));

        Assert.Equal(ErrorCode.FileNotFound, error.Code);
        Assert.False(File.Exists(request.OutputPath));
    }

    [Fact]
    public void MissingCatalogFile_ReportsFailureAndCreatesNoOutput()
    {
        var request = CreateRequest(new NativeFeeAdapter(FeeSchedule.Standard)) with { CatalogPath = PathOf("missing.json") };

        var error = Support.ExpectFailure(ReportWorkflow.Run(request));

        Assert.Equal(ErrorCode.FileNotFound, error.Code);
        Assert.False(File.Exists(request.OutputPath));
    }

    [Fact]
    public void InvalidPeriod_ReportsFailureAndCreatesNoOutput()
    {
        var request = CreateRequest(new NativeFeeAdapter(FeeSchedule.Standard));
        var invalid = request with { Settings = request.Settings with { PeriodStart = Support.Date("2026-11-01") } };

        var error = Support.ExpectFailure(ReportWorkflow.Run(invalid));

        Assert.Equal(ErrorCode.InvalidReportParameters, error.Code);
        Assert.False(File.Exists(invalid.OutputPath));
    }
}
