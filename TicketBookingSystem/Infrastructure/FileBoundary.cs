using TicketBookingSystem.Domain;

namespace TicketBookingSystem.Infrastructure;

public static class FileBoundary
{
    public static Result<T, AppError> Run<T>(string path, Func<Result<T, AppError>> action)
    {
        try
        {
            return action();
        }
        catch (FileNotFoundException)
        {
            return Result<T, AppError>.Fail(AppError.FileNotFound(path));
        }
        catch (DirectoryNotFoundException)
        {
            return Result<T, AppError>.Fail(AppError.FileNotFound(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Result<T, AppError>.Fail(AppError.FileAccessError(path, ex.Message));
        }
    }
}