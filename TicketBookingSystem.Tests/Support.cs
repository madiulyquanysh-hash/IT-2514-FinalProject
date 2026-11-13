using System.Globalization;
using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

internal static class Support
{
    public const string BookingsHeader = "reference,customer,eventName,quantity,promoCode,bookedOn";

    public static T ExpectSuccess<T>(Result<T, AppError> result) =>
        Assert.IsType<Result<T, AppError>.Success>(result).Value;

    public static AppError ExpectFailure<T>(Result<T, AppError> result) =>
        Assert.IsType<Result<T, AppError>.Failure>(result).Error;

    public static DateOnly Date(string text) =>
        DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static BookingService CreateService() =>
        new(
            new EventCatalog(new[]
            {
                new EventInfo("Tech Conference", 150m, 50),
                new EventInfo("Jazz Night", 45.5m, 10),
                new EventInfo("Sold Out Gala", 300m, 0)
            }),
            new PromoCatalog(new[]
            {
                new PromoCode("WELCOME10", 10),
                new PromoCode("NODISCOUNT", 0)
            }));

    public static BookingEntry Entry(
        string reference,
        string eventName,
        int quantity,
        string? promoCode = null,
        string date = "2026-10-01",
        string customer = "Customer") =>
        new(reference, customer, eventName, quantity, promoCode, Date(date));

    public static ProcessedBooking Processed(
        string reference,
        string eventName,
        int quantity,
        decimal amount,
        long fee,
        string date,
        int discount = 0,
        string customer = "Customer")
    {
        var entry = Entry(reference, eventName, quantity, null, date, customer);
        var receipt = new BookingReceipt(eventName, quantity, discount, amount, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        return new ProcessedBooking(entry, receipt, fee);
    }
}

internal sealed class StubFeeCalculator : IFeeCalculator
{
    private readonly long fee;

    public StubFeeCalculator(long fee)
    {
        this.fee = fee;
    }

    public int Calls { get; private set; }

    public Result<long, AppError> CalculateFeeCents(decimal amount, int quantity)
    {
        Calls++;
        return Result<long, AppError>.Ok(fee);
    }
}

internal sealed class FailingFeeCalculator : IFeeCalculator
{
    public Result<long, AppError> CalculateFeeCents(decimal amount, int quantity) =>
        Result<long, AppError>.Fail(AppError.NativeLibraryUnavailable("ticketfees", "simulated load failure"));
}

public abstract class TempDirectoryTestBase : IDisposable
{
    protected TempDirectoryTestBase()
    {
        Directory.CreateDirectory(Folder);
    }

    protected string Folder { get; } =
        Path.Combine(Path.GetTempPath(), "ticket-a8-" + Guid.NewGuid().ToString("N"));

    protected string PathOf(string name) => Path.Combine(Folder, name);

    protected string WriteFile(string name, string content)
    {
        var path = PathOf(name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                if (Directory.Exists(Folder))
                {
                    Directory.Delete(Folder, true);
                }

                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }
}