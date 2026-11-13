using TicketBookingSystem.Domain;

namespace TicketBookingSystem.Infrastructure;

public sealed class BookingDataStore
{
    private BookingDataStore(BookingData initial)
    {
        Current = initial;
    }

    public BookingData Current { get; private set; }

    public static Result<BookingDataStore, AppError> Open(string path) =>
        CatalogLoader.Load(path).Map(data => new BookingDataStore(data));

    public Result<BookingData, AppError> Reload(string path)
    {
        var loaded = CatalogLoader.Load(path);

        if (loaded is Result<BookingData, AppError>.Success success)
        {
            Current = success.Value;
        }

        return loaded;
    }
}