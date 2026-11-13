namespace TicketBookingSystem.Domain;

public static class ResultComposition
{
    public static Result<T, F> MapError<T, E, F>(this Result<T, E> result, Func<E, F> mapper) =>
        result.Match<Result<T, F>>(
            value => Result<T, F>.Ok(value),
            error => Result<T, F>.Fail(mapper(error)));

    public static (IReadOnlyList<T> Successes, IReadOnlyList<E> Failures) SplitResults<T, E>(
        this IEnumerable<Result<T, E>> results)
    {
        var snapshot = results.ToArray();
        IReadOnlyList<T> successes = snapshot
            .OfType<Result<T, E>.Success>()
            .Select(success => success.Value)
            .ToArray();
        IReadOnlyList<E> failures = snapshot
            .OfType<Result<T, E>.Failure>()
            .Select(failure => failure.Error)
            .ToArray();
        return (successes, failures);
    }
}