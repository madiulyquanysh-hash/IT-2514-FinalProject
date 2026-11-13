# Ticket Booking System: Final Project

Introduction to Functional Programming, academic year 2026 to 2027.

## Team and repository

| Madiuly Kuanysh | IT-2514 | all modules (individual project) |

- Repository URL: *fill in*
- Branch: *fill in*
- Final commit: *fill in*

## Problem, users, goals

A ticket office receives booking requests and cancellation requests as files. It must accept valid bookings, respect event capacity, apply promo codes, charge a service fee, handle cancellations with refunds, and produce reports for managers. Users are the ticket-office staff and the sales manager. The application is a console batch tool: the user prepares the input files, runs the program and reads the console output and the exported report.

## Setup, build, run, test

- .NET SDK 8.0 or newer, target `net8.0`
- Declared native platform: Windows 10 or 11, x64, 64-bit C compiler (MinGW-w64 gcc or MSVC)
- Working directory: the solution root (the folder with `TicketBookingSystem.sln` and `native/`)

```
native\build.bat
dotnet build
dotnet test
dotnet run --project TicketBookingSystem
```

Build the native library first. Without it the build succeeds but every native call reports `NativeLibraryUnavailable`. A prebuilt `native/bin/ticketfees.dll` (Windows x64) is included. Input paths are relative: `SampleData/` is copied next to the executable, and the report is written to `Output/` next to the executable.

## Workflows

| # | Workflow | Input | Business rules | Result |
|---|---|---|---|---|
| 1 | Book tickets | event name, quantity, promo code | event exists, quantity positive, within capacity, promo known (0 percent is valid), state allows confirmation, native service fee | receipt, or an explicit error |
| 2 | Batch booking and seat allocation | `bookings.csv` | every booking passes workflow 1 rules, then seats are reserved first come first served (date, then reference) | allocated bookings, waitlisted bookings, updated seat inventory |
| 3 | Cancel and refund | `cancellations.csv`, allocated bookings | booking must exist and be active, cancellation not before booking date, refund percent from a refund policy function, seats are released | refund receipts, rejected cancellations, updated inventory |
| 4 | Reports and export | active bookings after cancellations | period, minimum amount, scoring function | event revenue report, high-value report, exported `booking-reports.txt` |

Workflows 2 to 4 run as one pipeline: `ProcessBookings`, then `AllocateSeats`, then `ApplyCancellations`, then `BuildReports`, then export.

## Input files

| File | Format |
|---|---|
| `SampleData/catalog.json` | JSON: events and promo codes |
| `SampleData/bookings.csv` | `reference,customer,eventName,quantity,promoCode,bookedOn` |
| `SampleData/cancellations.csv` | `reference,cancelledOn,reason` |
| `SampleData/malformed.json`, `malformed-bookings.csv`, `malformed-cancellations.csv` | deliberately invalid files for failure demonstrations |

CSV rules: exact header, no quoted fields, date `yyyy-MM-dd`, blank lines skipped, duplicate booking references rejected. Any structural problem fails the whole load with `MalformedContent` and the line number.

## Architecture

```
Program.cs                  console boundary: paths, printing
Domain/                     pure logic, immutable records, no file or console access
  Result, Maybe             explicit outcomes and lookups
  AppError                  structured errors
  BookingService            workflow 1 (named stages)
  SeatInventory, SeatAllocator   immutable inventory, workflow 2
  Cancellation*, BookingLedger   workflow 3
  BookingPipeline           named pipeline stages
  BookingReports, ReportFormatter   two LINQ reports and text formatting
Infrastructure/             side effects only
  CatalogLoader, CsvFileLoader, LineSource, FileBoundary   file input
  ReportExporter                                           atomic file output
  NativeFeeAdapter, NativeMethods                          C library boundary
  ReportWorkflow                                           load, run pipeline, export
native/                     C source for the fee library
```

Data flow: files, then loaders (exceptions become `Result`), then the pure pipeline, then the formatter, then the exporter and the console.

## Requirement mapping

| Final requirement | Where |
|---|---|
| Two related domain models, two reports | events, bookings, cancellations; `EventRevenue`, `HighValue` |
| Pure core, immutable models | records, `ImmutableDictionary`; `SeatInventory` and `BookingLedger` return new instances |
| Pattern matching for a domain decision | `BookingWorkflow.TryConfirm`, `CancellationService.EnsureActive`, `RefundPolicies` |
| Pipeline with 3 or more named stages | `BookingPipeline.ProcessBookings`, `AllocateSeats`, `ApplyCancellations`, `BuildReports` |
| Higher-order function changing a rule | `Scoring.CreateScore` (curried), refund policy `Func<int,int>` |
| Safe operation sequence | `ValidatedBooking` factory, `PricedBooking` required for confirmation, `BookingWorkflow.TryConfirm`, `SeatInventory.Release`, `CancellationService`, stage records that require the previous stage |
| Result, at least 3 domain failures | `AppError` codes |
| Map and Bind in a real workflow | `BookingService`, `BookingPipeline`, `ReportWorkflow` |
| Missing lookups | `Maybe` in `EventCatalog.Find`, `PromoCatalog.Find`, `SeatInventory.Find`, `BookingLedger.Find` |
| Boundary exceptions | `FileBoundary`, `CatalogLoader`, `ReportExporter`, `NativeFeeAdapter` |
| Two custom extensions | `SplitResults`, `MapError`, `InPeriod` |
| GroupBy and aggregate, ordering with tie-breaker | `BookingReports.EventRevenue`, `HighValue` |
| `yield return`, deferred execution, materialisation | `BookingStream`, `CancellationStream`, `BookingBatchProcessor.Stream`, `LineSource` |
| Load JSON or CSV, export | loaders, `ReportExporter` |
| Native C library | `native/ticketfees.c`, `NativeFeeAdapter` |

## Error handling

Domain failures (category Domain) never throw: `InvalidQuantity`, `CapacityExceeded`, `EventNotFound`, `PromoCodeNotFound`, `InvalidState`, `BookingNotFound`, `InvalidCancellationDate`, `InvalidRefundPercent`, `InvalidFeeInput`, `InvalidReportParameters`. Operational failures (category Operational): `FileNotFound`, `MalformedContent`, `FileAccessError`, `NativeLibraryUnavailable`, `NativeBindingFailed`, `NativeCallRejected`. A native or file failure aborts the pipeline before export, so an existing report is untouched. The export writes a temporary file first and then moves it.

## Native library

| Item | Value |
|---|---|
| Export | `int64_t tf_service_fee_cents(int64_t amount_cents, int32_t quantity, int32_t rate_basis_points, int32_t per_ticket_cents)` |
| Binding | `NativeMethods.ServiceFeeCents`, `DllImport("ticketfees")`, Cdecl |
| Formula | `(amount_cents * rate_bp + 5000) / 10000 + quantity * per_ticket_cents` (half up), integer cents |
| Valid ranges | amount 0 to 1,000,000,000,000; quantity 1 to 1,000,000; rate 0 to 10,000; per-ticket 0 to 100,000 |
| Invalid input | returns `-1`; the adapter validates first |
| Manual example | `27000, 2, 250, 50` gives 675 + 100 = **775 cents** |
| Build | `native\build.bat` (gcc or cl), output `native/bin/ticketfees.dll` |
| Compiler and version | *gcc (MinGW-W64 x86_64-posix-seh) 13.2.0* |
| Integration difficulty and resolution | *describe the real difficulty you met* |

## Resource note

- Owners and release points: `LineSource` owns the `StreamReader` (`using var` in the iterator, released on finish, failure and early stop); `ReportExporter` owns the `StreamWriter` (`using` block); `File.ReadAllText` in `CatalogLoader` closes the file internally.
- Materialisation: CSV files are read lazily, then materialised once in `CsvFileLoader`; pipeline stages hold arrays and immutable collections.
- No caches, no event subscriptions and no long-lived growing collections; `BookingDataStore.Current` is replaced, not appended to.
- Trade-off: streaming uses little memory but repeats work on repeated enumeration; the snapshot costs memory but is stable and reusable. The snapshot is used because reports make several passes.

## Tests

xUnit project `TicketBookingSystem.Tests`. File tests use unique temporary folders and clean them up. Coverage: pure functions and domain validation (`Result`, `Maybe`, reports, scoring, extensions, `SeatInventory`, `SeatAllocator`, cancellations, parsers), complete workflows (`PipelineTests`, `WorkflowTests`), file handling (missing and malformed files for catalog, bookings and cancellations), deferred execution, resource lifetime, native integration (a real native call and injected load failures), failure propagation, empty, boundary and invalid cases.

Latest test outcome: run `dotnet test` and paste the summary (total 217, passed 217, failed 0, skipped 0).

## Limitations and future work

- Console batch tool, no interactive menu or GUI.
- Native library is built and tested for Windows x64 only.
- One currency, fixed fee schedule, event dates are not modelled.
- Waitlisted bookings are not promoted automatically after cancellations.
- Four workflows are implemented, which satisfies a team of up to 2 students.

## References

Official .NET documentation (LINQ, P/Invoke, System.Collections.Immutable) and the course lectures. No external application code was copied.

## Responsibilities

| Feature | Owner |
|---|---|
| All modules and tests | Madiuly Kuanysh |




"# IT-2514-FinalProject" 
