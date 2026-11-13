using System.Globalization;

namespace TicketBookingSystem.Domain;

public static class BookingStream
{
    public const string ExpectedHeader = "reference,customer,eventName,quantity,promoCode,bookedOn";

    public static IEnumerable<Result<BookingEntry, AppError>> ParseEntries(string source, IEnumerable<string> lines)
    {
        var lineNumber = 0;
        var references = new HashSet<string>(StringComparer.Ordinal);

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

            var parsed = ParseLine(source, lineNumber, line);

            if (parsed is Result<BookingEntry, AppError>.Success success && !references.Add(success.Value.Reference))
            {
                yield return Failure(source, $"Line {lineNumber}: reference '{success.Value.Reference}' is duplicated.");
                continue;
            }

            yield return parsed;
        }

        if (lineNumber == 0)
        {
            yield return Failure(source, "The file is empty; the header line is missing.");
        }
    }

    private static Result<BookingEntry, AppError> ParseLine(string source, int lineNumber, string line)
    {
        var fields = line.Split(',');

        if (fields.Length != 6)
        {
            return Failure(source, $"Line {lineNumber}: expected 6 fields but found {fields.Length}.");
        }

        var reference = fields[0].Trim();
        var customer = fields[1].Trim();

        if (reference.Length == 0 || customer.Length == 0)
        {
            return Failure(source, $"Line {lineNumber}: reference and customer are required.");
        }

        if (!int.TryParse(fields[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
        {
            return Failure(source, $"Line {lineNumber}: quantity '{fields[3].Trim()}' is not an integer.");
        }

        if (!DateOnly.TryParseExact(fields[5].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var bookedOn))
        {
            return Failure(source, $"Line {lineNumber}: date '{fields[5].Trim()}' must use the format yyyy-MM-dd.");
        }

        var promoCode = fields[4].Trim();

        return Result<BookingEntry, AppError>.Ok(new BookingEntry(
            reference,
            customer,
            fields[2].Trim(),
            quantity,
            promoCode.Length == 0 ? null : promoCode,
            bookedOn));
    }

    private static Result<BookingEntry, AppError> Failure(string source, string detail) =>
        Result<BookingEntry, AppError>.Fail(AppError.MalformedContent(source, detail));
}
