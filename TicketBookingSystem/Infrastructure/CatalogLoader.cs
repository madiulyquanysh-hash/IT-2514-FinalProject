using System.Text.Json;
using TicketBookingSystem.Domain;

namespace TicketBookingSystem.Infrastructure;

public sealed record BookingData(EventCatalog Events, PromoCatalog Promos);

public static class CatalogLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static Result<BookingData, AppError> Load(string path)
    {
        string text;

        try
        {
            text = File.ReadAllText(path);
        }
        catch (FileNotFoundException)
        {
            return Failure(AppError.FileNotFound(path));
        }
        catch (DirectoryNotFoundException)
        {
            return Failure(AppError.FileNotFound(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Failure(AppError.FileAccessError(path, ex.Message));
        }

        CatalogDto? dto;

        try
        {
            dto = JsonSerializer.Deserialize<CatalogDto>(text, SerializerOptions);
        }
        catch (JsonException ex)
        {
            return Failure(AppError.MalformedContent(path, ex.Message));
        }

        if (dto is null)
        {
            return Failure(AppError.MalformedContent(path, "The JSON document does not contain a catalog object."));
        }

        return BuildData(path, dto);
    }

    private static Result<BookingData, AppError> BuildData(string path, CatalogDto dto)
    {
        if (dto.Events is null)
        {
            return Failure(AppError.MalformedContent(path, "Property 'events' is missing."));
        }

        if (dto.PromoCodes is null)
        {
            return Failure(AppError.MalformedContent(path, "Property 'promoCodes' is missing."));
        }

        var events = new List<EventInfo>();
        var eventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < dto.Events.Count; i++)
        {
            var item = dto.Events[i];

            if (item is null
                || string.IsNullOrWhiteSpace(item.Name)
                || item.UnitPrice is null
                || item.AvailableCapacity is null)
            {
                return Failure(AppError.MalformedContent(path, $"Event at index {i} is missing a required field."));
            }

            if (item.UnitPrice <= 0 || item.AvailableCapacity < 0)
            {
                return Failure(AppError.MalformedContent(path, $"Event at index {i} has an invalid price or capacity."));
            }

            var name = item.Name.Trim();

            if (!eventNames.Add(name))
            {
                return Failure(AppError.MalformedContent(path, $"Event '{name}' is defined more than once."));
            }

            events.Add(new EventInfo(name, item.UnitPrice.Value, item.AvailableCapacity.Value));
        }

        var promoCodes = new List<PromoCode>();
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < dto.PromoCodes.Count; i++)
        {
            var item = dto.PromoCodes[i];

            if (item is null || string.IsNullOrWhiteSpace(item.Code) || item.DiscountPercent is null)
            {
                return Failure(AppError.MalformedContent(path, $"Promo code at index {i} is missing a required field."));
            }

            if (item.DiscountPercent < 0 || item.DiscountPercent > 100)
            {
                return Failure(AppError.MalformedContent(path, $"Promo code at index {i} has a discount outside 0 to 100."));
            }

            var code = item.Code.Trim();

            if (!codes.Add(code))
            {
                return Failure(AppError.MalformedContent(path, $"Promo code '{code}' is defined more than once."));
            }

            promoCodes.Add(new PromoCode(code, item.DiscountPercent.Value));
        }

        var data = new BookingData(new EventCatalog(events), new PromoCatalog(promoCodes));
        return Result<BookingData, AppError>.Ok(data);
    }

    private static Result<BookingData, AppError> Failure(AppError error) =>
        Result<BookingData, AppError>.Fail(error);

    internal sealed class CatalogDto
    {
        public List<EventDto?>? Events { get; set; }
        public List<PromoDto?>? PromoCodes { get; set; }
    }

    internal sealed class EventDto
    {
        public string? Name { get; set; }
        public decimal? UnitPrice { get; set; }
        public int? AvailableCapacity { get; set; }
    }

    internal sealed class PromoDto
    {
        public string? Code { get; set; }
        public int? DiscountPercent { get; set; }
    }
}