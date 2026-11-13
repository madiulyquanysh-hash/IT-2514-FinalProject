using System.Globalization;

namespace TicketBookingSystem.Domain;

public static class CancellationStream
{
    public const string ExpectedHeader = "reference,cancelledOn,reason";

    public static IEnumerable<Result<CancellationEntry, AppError>> ParseEntries(string source, IEnumerable<string> lines)
    {
        var lineNumber = 0;

        foreach (var line in lines)
        {
            lineNumber++;

            if (lineNumber == 1)
            {
                if (!string.Equals(line.Trim(), ExpectedHeader, StringComparison.OrdinalIgnoreCase))
                {
                    yield return Failure(source, $"Line 1: expected header '{ExpectedHeader}'.");
                    yield break;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            yield return ParseLine(source, lineNumber, line);
        }

        if (lineNumber == 0)
        {
            yield return Failure(source, "The file is empty; the header line is missing.");
        }
    }

    private static Result<CancellationEntry, AppError> ParseLine(string source, int lineNumber, string line)
    {
        var fields = line.Split(',');

        if (fields.Length != 3)
        {
            return Failure(source, $"Line {lineNumber}: expected 3 fields but found {fields.Length}.");
        }

        var reference = fields[0].Trim();

        if (reference.Length == 0)
        {
            return Failure(source, $"Line {lineNumber}: reference is required.");
        }

        if (!DateOnly.TryParseExact(fields[1].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var cancelledOn))
        {
            return Failure(source, $"Line {lineNumber}: date '{fields[1].Trim()}' must use the format yyyy-MM-dd.");
        }

        var reason = fields[2].Trim();

        return Result<CancellationEntry, AppError>.Ok(
            new CancellationEntry(reference, cancelledOn, reason.Length == 0 ? "unspecified" : reason));
    }

    private static Result<CancellationEntry, AppError> Failure(string source, string detail) =>
        Result<CancellationEntry, AppError>.Fail(AppError.MalformedContent(source, detail));
}
