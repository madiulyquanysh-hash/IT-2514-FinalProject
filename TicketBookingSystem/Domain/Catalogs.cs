namespace TicketBookingSystem.Domain;

public sealed record EventInfo(string Name, decimal UnitPrice, int AvailableCapacity);

public sealed record PromoCode(string Code, int DiscountPercent);

public sealed class EventCatalog
{
    private readonly IReadOnlyDictionary<string, EventInfo> events;

    public EventCatalog(IEnumerable<EventInfo> items)
    {
        var map = new Dictionary<string, EventInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!map.TryAdd(item.Name.Trim(), item))
            {
                throw new ArgumentException($"Duplicate event name '{item.Name}'.", nameof(items));
            }
        }

        events = map;
    }

    public int Count => events.Count;

    public IEnumerable<EventInfo> All => events.Values;

    public Maybe<EventInfo> Find(string? name) =>
        name is not null && events.TryGetValue(name.Trim(), out var found)
            ? Maybe.Some(found)
            : Maybe.None<EventInfo>();
}

public sealed class PromoCatalog
{
    private readonly IReadOnlyDictionary<string, int> discounts;

    public PromoCatalog(IEnumerable<PromoCode> codes)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var promo in codes)
        {
            if (promo.DiscountPercent is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(codes), $"Discount for '{promo.Code}' must be between 0 and 100.");
            }

            if (!map.TryAdd(promo.Code.Trim(), promo.DiscountPercent))
            {
                throw new ArgumentException($"Duplicate promo code '{promo.Code}'.", nameof(codes));
            }
        }

        discounts = map;
    }

    public int Count => discounts.Count;

    public Maybe<int> Find(string? code) =>
        code is not null && discounts.TryGetValue(code.Trim(), out var percent)
            ? Maybe.Some(percent)
            : Maybe.None<int>();
}
