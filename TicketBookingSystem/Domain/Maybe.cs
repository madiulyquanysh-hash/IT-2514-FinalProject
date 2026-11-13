namespace TicketBookingSystem.Domain;

public abstract record Maybe<T>
{
    private Maybe() { }

    public abstract TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone);

    public sealed record Some : Maybe<T>
    {
        public T Value { get; }

        public Some(T value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value), "Some requires a value. Use None for absence.");
            }

            Value = value;
        }

        public override TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone) =>
            onSome(Value);
    }

    public sealed record None : Maybe<T>
    {
        public override TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone) =>
            onNone();
    }
}

public static class Maybe
{
    public static Maybe<T> Some<T>(T value) => new Maybe<T>.Some(value);

    public static Maybe<T> None<T>() => new Maybe<T>.None();

    public static Maybe<T> FromReference<T>(T? value) where T : class =>
        value is null ? None<T>() : Some(value);
}

public static class MaybeExtensions
{
    public static Result<T, E> ToResult<T, E>(this Maybe<T> maybe, Func<E> onMissing) =>
        maybe.Match<Result<T, E>>(
            value => Result<T, E>.Ok(value),
            () => Result<T, E>.Fail(onMissing()));
}