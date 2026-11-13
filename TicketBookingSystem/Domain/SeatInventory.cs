using System.Collections.Immutable;

namespace TicketBookingSystem.Domain;

public sealed record SeatLevel(string EventName, int Capacity, int Remaining);

public sealed class SeatInventory
{
    private readonly ImmutableDictionary<string, SeatLevel> levels;

    private SeatInventory(ImmutableDictionary<string, SeatLevel> levels)
    {
        this.levels = levels;
    }

    public static SeatInventory FromCatalog(EventCatalog catalog) =>
        new(catalog.All.ToImmutableDictionary(
            info => info.Name,
            info => new SeatLevel(info.Name, info.AvailableCapacity, info.AvailableCapacity),
            StringComparer.OrdinalIgnoreCase));

    public IReadOnlyList<SeatLevel> Levels =>
        levels.Values.OrderBy(level => level.EventName, StringComparer.Ordinal).ToArray();

    public Maybe<SeatLevel> Find(string eventName) =>
        levels.TryGetValue(eventName, out var level)
            ? Maybe.Some(level)
            : Maybe.None<SeatLevel>();

    public Result<SeatInventory, AppError> Reserve(string eventName, int quantity) =>
        Find(eventName)
            .ToResult(() => AppError.EventNotFound(eventName))
            .Bind(level => EnsurePositive(level, quantity))
            .Bind(level => EnsureAvailable(level, quantity))
            .Map(level => With(level with { Remaining = level.Remaining - quantity }));

    public Result<SeatInventory, AppError> Release(string eventName, int quantity) =>
        Find(eventName)
            .ToResult(() => AppError.EventNotFound(eventName))
            .Bind(level => EnsurePositive(level, quantity))
            .Bind(level => EnsureReleasable(level, quantity))
            .Map(level => With(level with { Remaining = level.Remaining + quantity }));

    private SeatInventory With(SeatLevel level) =>
        new(levels.SetItem(level.EventName, level));

    private static Result<SeatLevel, AppError> EnsurePositive(SeatLevel level, int quantity) =>
        quantity > 0
            ? Result<SeatLevel, AppError>.Ok(level)
            : Result<SeatLevel, AppError>.Fail(AppError.InvalidQuantity(level.EventName));

    private static Result<SeatLevel, AppError> EnsureAvailable(SeatLevel level, int quantity) =>
        quantity <= level.Remaining
            ? Result<SeatLevel, AppError>.Ok(level)
            : Result<SeatLevel, AppError>.Fail(AppError.CapacityExceeded(level.EventName));

    private static Result<SeatLevel, AppError> EnsureReleasable(SeatLevel level, int quantity) =>
        (long)level.Remaining + quantity <= level.Capacity
            ? Result<SeatLevel, AppError>.Ok(level)
            : Result<SeatLevel, AppError>.Fail(
                AppError.InvalidState("Cannot release more seats than were reserved.", level.EventName));
}
