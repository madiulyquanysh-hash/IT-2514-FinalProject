using TicketBookingSystem.Domain;

namespace TicketBookingSystem.Infrastructure;

public static class BookingFileLoader
{
    public static Result<IReadOnlyList<BookingEntry>, AppError> Load(string path) =>
        CsvFileLoader.Load<BookingEntry>(path, BookingStream.ParseEntries);
}
