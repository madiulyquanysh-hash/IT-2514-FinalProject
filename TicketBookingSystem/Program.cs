using TicketBookingSystem.Domain;
using TicketBookingSystem.Infrastructure;

var sampleDirectory = Path.Combine(AppContext.BaseDirectory, "SampleData");
var catalogPath = Path.Combine(sampleDirectory, "catalog.json");
var malformedPath = Path.Combine(sampleDirectory, "malformed.json");
var missingPath = Path.Combine(sampleDirectory, "does-not-exist.json");
var bookingsPath = Path.Combine(sampleDirectory, "bookings.csv");
var cancellationsPath = Path.Combine(sampleDirectory, "cancellations.csv");
var malformedBookingsPath = Path.Combine(sampleDirectory, "malformed-bookings.csv");
var malformedCancellationsPath = Path.Combine(sampleDirectory, "malformed-cancellations.csv");
var missingBookingsPath = Path.Combine(sampleDirectory, "missing-bookings.csv");
var missingCancellationsPath = Path.Combine(sampleDirectory, "missing-cancellations.csv");
var outputDirectory = Path.Combine(AppContext.BaseDirectory, "Output");
var reportPath = Path.Combine(outputDirectory, "booking-reports.txt");

Section("Startup");
var opened = BookingDataStore.Open(catalogPath);

if (opened is not Result<BookingDataStore, AppError>.Success openedSuccess)
{
    Console.WriteLine(Describe(opened, _ => string.Empty));
    return 1;
}

var store = openedSuccess.Value;
Console.WriteLine($"Catalog loaded: {store.Current.Events.Count} events, {store.Current.Promos.Count} promo codes.");

var service = new BookingService(store.Current.Events, store.Current.Promos);

Section("Workflow 1: book tickets (find, validate, price, confirm)");
Report("Two tickets, no promo", service.Book(new BookingCommand("Tech Conference", 2)), r => r.Describe());
Report("Two tickets, promo WELCOME10", service.Book(new BookingCommand("Tech Conference", 2, "WELCOME10")), r => r.Describe());
Report("Two tickets, promo NODISCOUNT (0 percent)", service.Book(new BookingCommand("Jazz Night", 2, "NODISCOUNT")), r => r.Describe());

Section("Workflow 1: expected domain failures");
Report("Invalid quantity", service.Book(new BookingCommand("Tech Conference", 0)), r => r.Describe());
Report("Capacity exceeded", service.Book(new BookingCommand("Jazz Night", 11)), r => r.Describe());
Report("Sold out event", service.Book(new BookingCommand("Sold Out Gala", 1)), r => r.Describe());
Report("Missing event", service.Book(new BookingCommand("Unknown Event", 1)), r => r.Describe());
Report("Unknown promo code", service.Book(new BookingCommand("Jazz Night", 1, "BOGUS")), r => r.Describe());

Section("Safe operation sequence");
var confirmAgain = service
    .Prepare(new BookingCommand("Jazz Night", 1))
    .Bind(priced => service.Confirm(priced, new BookingState.Confirmed(priced.Booking, DateTime.UtcNow)));
Report("Confirm an already confirmed booking", confirmAgain, r => r.Describe());

var confirmCancelled = service
    .Prepare(new BookingCommand("Jazz Night", 1))
    .Bind(priced => service.Confirm(priced, new BookingState.Cancelled("Customer cancelled")));
Report("Confirm a cancelled booking", confirmCancelled, r => r.Describe());

Report(
    "Release seats that were never reserved",
    SeatInventory.FromCatalog(store.Current.Events).Release("Jazz Night", 1),
    inventory => $"{inventory.Levels.Count} events");

Section("Lookups");
Console.WriteLine(store.Current.Events.Find("Tech Conference").Match(
    e => $"Event present: {e.Name}, capacity {e.AvailableCapacity}",
    () => "Event absent"));
Console.WriteLine(store.Current.Events.Find("Unknown Event").Match(
    e => $"Event present: {e.Name}, capacity {e.AvailableCapacity}",
    () => "Event absent: Unknown Event"));
Console.WriteLine(store.Current.Events.Find("Sold Out Gala").Match(
    e => $"Event present: {e.Name}, capacity {e.AvailableCapacity} (zero capacity is still present)",
    () => "Event absent"));
Console.WriteLine(store.Current.Promos.Find("NODISCOUNT").Match(
    p => $"Promo present: {p} percent discount (zero is still present)",
    () => "Promo absent"));
Console.WriteLine(store.Current.Promos.Find("BOGUS").Match(
    p => $"Promo present: {p} percent discount",
    () => "Promo absent: BOGUS"));

Section("File failures");
var before = store.Current;
Report("Reload missing file", store.Reload(missingPath), d => $"{d.Events.Count} events");
Report("Reload malformed file", store.Reload(malformedPath), d => $"{d.Events.Count} events");
Console.WriteLine($"Stored data unchanged after failed reloads: {ReferenceEquals(before, store.Current)}");

Section("Valid reload");
Report("Reload valid file", store.Reload(catalogPath), d => $"{d.Events.Count} events, {d.Promos.Count} promo codes");

Section("Earlier pipeline (Assignment 6)");
var legacyValidation = ValidatedBooking.Create(new BookingRequest("Tech Conference", 2, 150.0m, 50));
BookingState legacyState = legacyValidation switch
{
    ValidationResult.Valid valid => new BookingState.Pending(valid.Booking),
    ValidationResult.Invalid invalid => new BookingState.Cancelled(invalid.Reason),
    _ => new BookingState.Cancelled("Unknown validation state.")
};
Console.WriteLine($"Initial state: {DescribeState(legacyState)}");
var legacyConfirmed = BookingWorkflow.Process(legacyValidation, legacyState);
Console.WriteLine($"After processing: {DescribeState(legacyConfirmed)}");
var legacyRepeated = BookingWorkflow.Process(legacyValidation, legacyConfirmed);
Console.WriteLine($"After repeated processing: {DescribeState(legacyRepeated)}");
var legacyRejected = BookingWorkflow.Process(
    ValidatedBooking.Create(new BookingRequest("Tech Conference", 100, 150.0m, 50)),
    new BookingState.Cancelled("Initial"));
Console.WriteLine($"Rejected request: {DescribeState(legacyRejected)}");

Section("Native fee adapter (C library through P/Invoke)");
var fees = new NativeFeeAdapter(FeeSchedule.Standard);
Report("Fee for 270.00 and 2 tickets, expected 775 cents", fees.CalculateFeeCents(270.00m, 2), cents => $"{cents} cents");
Report("Fee for a negative amount", fees.CalculateFeeCents(-1m, 2), cents => $"{cents} cents");

Section("Configured functions (currying and partial application)");
var revenueFocused = Scoring.CreateScore(new ScoreConfig(1, 10, 2));
var volumeFocused = Scoring.CreateScore(new ScoreConfig(10, 1, 0));
Console.WriteLine("Revenue-focused score: 1 point per ticket, 10 per full 100 units, minus 2 per discount percent.");
Console.WriteLine("Volume-focused score: 10 points per ticket, 1 per full 100 units, no discount penalty.");
Console.WriteLine("Refund policies: Flexible (100 percent up to 7 days, 50 percent up to 14 days) and Strict (100 percent up to 2 days).");

var settings = new PipelineSettings(
    new DateOnly(2026, 10, 1),
    new DateOnly(2026, 10, 31),
    100m,
    revenueFocused,
    RefundPolicies.Flexible);

var request = new WorkflowRequest(
    catalogPath,
    bookingsPath,
    cancellationsPath,
    reportPath,
    settings,
    fees);

var workflow = ReportWorkflow.Run(request);

if (workflow is Result<WorkflowOutput, AppError>.Success workflowSuccess)
{
    var pipeline = workflowSuccess.Value.Result;

    Section("Workflow 2: batch booking and seat allocation");
    Console.WriteLine($"Accepted by booking rules: {pipeline.Batch.Accepted.Count}, rejected: {pipeline.Batch.Rejected.Count}");

    foreach (var rejectedBooking in pipeline.Batch.Rejected)
    {
        Console.WriteLine($"  Rejected {rejectedBooking.Entry.Reference}: {rejectedBooking.Error.Describe()}");
    }

    Console.WriteLine($"Allocated: {pipeline.Allocation.Allocated.Count}, waitlisted: {pipeline.Allocation.Waitlisted.Count}");

    foreach (var waitlisted in pipeline.Allocation.Waitlisted)
    {
        Console.WriteLine($"  Waitlisted {waitlisted.Entry.Reference}: {waitlisted.Error.Describe()}");
    }

    Console.WriteLine("Seat inventory after allocation:");
    PrintLines(ReportFormatter.SeatLines(pipeline.Allocation.Inventory));

    Section("Workflow 3: cancellations and refunds (flexible policy)");
    PrintLines(ReportFormatter.CancellationLines(pipeline.Cancellations.Receipts));

    foreach (var rejectedCancellation in pipeline.Cancellations.Rejected)
    {
        Console.WriteLine($"  Rejected {rejectedCancellation.Entry.Reference} on {rejectedCancellation.Entry.CancelledOn:yyyy-MM-dd}: {rejectedCancellation.Error.Describe()}");
    }

    var strictBatch = CancellationBatchProcessor.Run(
        BookingLedger.From(pipeline.Allocation.Allocated),
        pipeline.Input.Cancellations,
        RefundPolicies.Strict);

    Console.WriteLine("Same cancellations under the strict policy:");
    PrintLines(ReportFormatter.CancellationLines(strictBatch.Receipts));

    Console.WriteLine("Seat inventory after cancellations:");
    PrintLines(ReportFormatter.SeatLines(pipeline.Inventory));

    Section("Workflow 4: reports and export (active bookings only)");
    Console.WriteLine("Report 1: event revenue");
    PrintLines(ReportFormatter.EventRevenueLines(pipeline.Revenue));

    Console.WriteLine("Report 2: high-value bookings, revenue-focused score");
    PrintLines(ReportFormatter.HighValueLines(pipeline.HighValue));

    var volumeLines = BookingReports
        .HighValue(
            pipeline.Cancellations.Ledger.ActiveBookings,
            settings.PeriodStart,
            settings.PeriodEnd,
            settings.MinimumAmount,
            volumeFocused)
        .Match<IReadOnlyList<string>>(
            report => ReportFormatter.HighValueLines(report),
            error => new[] { $"FAILED {error.Describe()}" });

    Console.WriteLine("Report 2: same snapshot, volume-focused score");
    PrintLines(volumeLines);

    Console.WriteLine($"Reports exported to: {workflowSuccess.Value.ExportedPath}");
}
else
{
    Section("Integrated workflow");
    Console.WriteLine(Describe(workflow, _ => string.Empty));
}

Section("Deferred execution and materialisation");

if (BookingFileLoader.Load(bookingsPath) is Result<IReadOnlyList<BookingEntry>, AppError>.Success loadedEntries)
{
    var processor = new BookingProcessor(service, fees);
    var processedCount = 0;
    var lazyStream = BookingBatchProcessor.Stream(loadedEntries.Value, entry =>
    {
        processedCount++;
        return processor.Process(entry);
    });

    Console.WriteLine($"Stream created, entries processed: {processedCount}");
    _ = lazyStream.First();
    Console.WriteLine($"After taking the first item: {processedCount}");
    var snapshot = lazyStream.ToArray();
    Console.WriteLine($"After materialising {snapshot.Length} items: {processedCount} (the first entry was processed twice)");
    _ = snapshot.Count(item => item is Result<ProcessedBooking, RejectedBooking>.Success);
    _ = snapshot.Length;
    Console.WriteLine($"After reusing the snapshot: {processedCount} (no repeated processing)");
}

Section("Workflow failure paths");
var unavailableFees = new NativeFeeAdapter(
    FeeSchedule.Standard,
    (_, _, _, _) => throw new DllNotFoundException("ticketfees"));

var unavailablePath = Path.Combine(outputDirectory, "unavailable.txt");
Report("Native library unavailable", ReportWorkflow.Run(request with { Fees = unavailableFees, OutputPath = unavailablePath }), o => o.ExportedPath);
Console.WriteLine($"Output created after native failure: {File.Exists(unavailablePath)}");

var missingBookingsOutput = Path.Combine(outputDirectory, "missing-bookings.txt");
Report("Missing bookings file", ReportWorkflow.Run(request with { BookingsPath = missingBookingsPath, OutputPath = missingBookingsOutput }), o => o.ExportedPath);
Console.WriteLine($"Output created after missing bookings file: {File.Exists(missingBookingsOutput)}");

var missingCancellationsOutput = Path.Combine(outputDirectory, "missing-cancellations.txt");
Report("Missing cancellations file", ReportWorkflow.Run(request with { CancellationsPath = missingCancellationsPath, OutputPath = missingCancellationsOutput }), o => o.ExportedPath);
Console.WriteLine($"Output created after missing cancellations file: {File.Exists(missingCancellationsOutput)}");

var malformedBookingsOutput = Path.Combine(outputDirectory, "malformed-bookings.txt");
Report("Malformed bookings file", ReportWorkflow.Run(request with { BookingsPath = malformedBookingsPath, OutputPath = malformedBookingsOutput }), o => o.ExportedPath);
Console.WriteLine($"Output created after malformed bookings file: {File.Exists(malformedBookingsOutput)}");

var malformedCancellationsOutput = Path.Combine(outputDirectory, "malformed-cancellations.txt");
Report("Malformed cancellations file", ReportWorkflow.Run(request with { CancellationsPath = malformedCancellationsPath, OutputPath = malformedCancellationsOutput }), o => o.ExportedPath);
Console.WriteLine($"Output created after malformed cancellations file: {File.Exists(malformedCancellationsOutput)}");

var invalidPeriodOutput = Path.Combine(outputDirectory, "invalid-period.txt");
var invalidSettings = settings with { PeriodStart = new DateOnly(2026, 11, 1) };
Report("Invalid report period", ReportWorkflow.Run(request with { Settings = invalidSettings, OutputPath = invalidPeriodOutput }), o => o.ExportedPath);
Console.WriteLine($"Output created after invalid period: {File.Exists(invalidPeriodOutput)}");

return 0;

static void Section(string title) =>
    Console.WriteLine($"{Environment.NewLine}=== {title} ===");

static void PrintLines(IEnumerable<string> lines)
{
    foreach (var line in lines)
    {
        Console.WriteLine($"  {line}");
    }
}

static string Describe<T>(Result<T, AppError> result, Func<T, string> describe) =>
    result.Match(
        value => $"OK {describe(value)}",
        error => $"FAILED {error.Describe()}");

static void Report<T>(string title, Result<T, AppError> result, Func<T, string> describe) =>
    Console.WriteLine($"{title}: {Describe(result, describe)}");

static string DescribeState(BookingState state) =>
    state switch
    {
        BookingState.Pending pending => $"Pending ({pending.Booking.Quantity} x {pending.Booking.EventName})",
        BookingState.Confirmed confirmed => $"Confirmed ({confirmed.Booking.Quantity} x {confirmed.Booking.EventName})",
        BookingState.Cancelled cancelled => $"Cancelled ({cancelled.Reason})",
        _ => "Unknown"
    };
