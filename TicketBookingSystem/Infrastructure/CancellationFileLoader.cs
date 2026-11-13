using TicketBookingSystem.Domain;

namespace TicketBookingSystem.Infrastructure;

public static class CancellationFileLoader
{
    public static Result<IReadOnlyList<CancellationEntry>, AppError> Load(string path) =>
        CsvFileLoader.Load<CancellationEntry>(path, CancellationStream.ParseEntries);
}
