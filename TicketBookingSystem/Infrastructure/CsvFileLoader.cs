using TicketBookingSystem.Domain;

namespace TicketBookingSystem.Infrastructure;

public static class CsvFileLoader
{
    public static Result<IReadOnlyList<T>, AppError> Load<T>(
        string path,
        Func<string, IEnumerable<string>, IEnumerable<Result<T, AppError>>> parse) =>
        FileBoundary.Run(path, () => ReadAll(path, parse));

    private static Result<IReadOnlyList<T>, AppError> ReadAll<T>(
        string path,
        Func<string, IEnumerable<string>, IEnumerable<Result<T, AppError>>> parse)
    {
        var items = new List<T>();
        var lines = LineSource.ReadLines(() => new StreamReader(path));

        foreach (var parsed in parse(path, lines))
        {
            if (parsed is Result<T, AppError>.Failure failure)
            {
                return Result<IReadOnlyList<T>, AppError>.Fail(failure.Error);
            }

            if (parsed is Result<T, AppError>.Success success)
            {
                items.Add(success.Value);
            }
        }

        return Result<IReadOnlyList<T>, AppError>.Ok(items);
    }
}
