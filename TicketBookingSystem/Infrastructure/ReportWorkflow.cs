using TicketBookingSystem.Domain;

namespace TicketBookingSystem.Infrastructure;

public sealed record WorkflowRequest(
    string CatalogPath,
    string BookingsPath,
    string CancellationsPath,
    string OutputPath,
    PipelineSettings Settings,
    IFeeCalculator Fees);

public sealed record WorkflowOutput(PipelineResult Result, string ExportedPath);

public static class ReportWorkflow
{
    public static Result<WorkflowOutput, AppError> Run(WorkflowRequest request) =>
        LoadInput(request)
            .Bind(input => BookingPipeline.Run(input))
            .Bind(result => ReportExporter
                .Export(request.OutputPath, ReportFormatter.CombinedLines(result))
                .Map(path => new WorkflowOutput(result, path)));

    private static Result<PipelineInput, AppError> LoadInput(WorkflowRequest request) =>
        CatalogLoader.Load(request.CatalogPath)
            .Bind(data => BookingFileLoader.Load(request.BookingsPath)
                .Bind(bookings => CancellationFileLoader.Load(request.CancellationsPath)
                    .Map(cancellations => new PipelineInput(
                        data.Events,
                        data.Promos,
                        bookings,
                        cancellations,
                        request.Fees,
                        request.Settings))));
}
