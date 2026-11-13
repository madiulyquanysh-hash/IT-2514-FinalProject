namespace TicketBookingSystem.Domain;

public abstract record Result<T, E>
{
    private Result() { }

    public abstract TResult Match<TResult>(Func<T, TResult> onSuccess, Func<E, TResult> onFailure);

    public static Result<T, E> Ok(T value) => new Success(value);

    public static Result<T, E> Fail(E error) => new Failure(error);

    public sealed record Success : Result<T, E>
    {
        public T Value { get; }

        public Success(T value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value), "A successful result requires a payload.");
            }

            Value = value;
        }

        public override TResult Match<TResult>(Func<T, TResult> onSuccess, Func<E, TResult> onFailure) =>
            onSuccess(Value);
    }

    public sealed record Failure : Result<T, E>
    {
        public E Error { get; }

        public Failure(E error)
        {
            if (error is null)
            {
                throw new ArgumentNullException(nameof(error), "A failed result requires an error.");
            }

            Error = error;
        }

        public override TResult Match<TResult>(Func<T, TResult> onSuccess, Func<E, TResult> onFailure) =>
            onFailure(Error);
    }
}

public static class ResultExtensions
{
    public static Result<U, E> Map<T, U, E>(this Result<T, E> result, Func<T, U> mapper) =>
        result.Match<Result<U, E>>(
            value => Result<U, E>.Ok(mapper(value)),
            error => Result<U, E>.Fail(error));

    public static Result<U, E> Bind<T, U, E>(this Result<T, E> result, Func<T, Result<U, E>> binder) =>
        result.Match<Result<U, E>>(
            value => binder(value),
            error => Result<U, E>.Fail(error));
}
