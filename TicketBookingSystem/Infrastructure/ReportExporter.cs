using System.Text;
using TicketBookingSystem.Domain;

namespace TicketBookingSystem.Infrastructure;

public static class ReportExporter
{
    public static Result<string, AppError> Export(string path, IReadOnlyList<string> lines)
    {
        var temporaryPath = path + ".tmp";

        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(temporaryPath, false, new UTF8Encoding(false)))
            {
                foreach (var line in lines)
                {
                    writer.WriteLine(line);
                }
            }

            File.Move(temporaryPath, path, true);
            return Result<string, AppError>.Ok(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var removed = RemoveTemporary(temporaryPath);
            var detail = removed ? ex.Message : ex.Message + " The temporary file could not be removed.";
            return Result<string, AppError>.Fail(AppError.FileAccessError(path, detail));
        }
    }

    private static bool RemoveTemporary(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}