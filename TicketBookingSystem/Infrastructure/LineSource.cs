namespace TicketBookingSystem.Infrastructure;

public static class LineSource
{
    public static IEnumerable<string> ReadLines(Func<TextReader> openReader)
    {
        using var reader = openReader();

        while (true)
        {
            var line = reader.ReadLine();

            if (line is null)
            {
                yield break;
            }

            yield return line;
        }
    }
}