namespace TicketBookingSystem.Domain;

public enum ErrorCode
{
    InvalidEventName,
    InvalidQuantity,
    CapacityExceeded,
    InvalidUnitPrice,
    EventNotFound,
    PromoCodeNotFound,
    InvalidState,
    FileNotFound,
    MalformedContent,
    FileAccessError,
    InvalidFeeInput,
    InvalidReportParameters,
    NativeLibraryUnavailable,
    NativeBindingFailed,
    NativeCallRejected,
    BookingNotFound,
    InvalidCancellationDate,
    InvalidRefundPercent
}

public enum ErrorCategory
{
    Domain,
    Operational
}

public sealed record AppError(ErrorCode Code, ErrorCategory Category, string Message, string Subject)
{
    public string Describe() => $"{Code} ({Category}): {Message} [{Subject}]";

    public static AppError InvalidEventName() =>
        new(ErrorCode.InvalidEventName, ErrorCategory.Domain, "Event name cannot be empty.", "EventName");

    public static AppError InvalidQuantity(string eventName) =>
        new(ErrorCode.InvalidQuantity, ErrorCategory.Domain, "Quantity must be greater than zero.", eventName);

    public static AppError CapacityExceeded(string eventName) =>
        new(ErrorCode.CapacityExceeded, ErrorCategory.Domain, "Requested quantity exceeds available capacity.", eventName);

    public static AppError InvalidUnitPrice(string eventName) =>
        new(ErrorCode.InvalidUnitPrice, ErrorCategory.Domain, "Unit price must be positive.", eventName);

    public static AppError EventNotFound(string eventName) =>
        new(ErrorCode.EventNotFound, ErrorCategory.Domain, $"Event '{eventName}' was not found in the catalog.", eventName);

    public static AppError PromoCodeNotFound(string code) =>
        new(ErrorCode.PromoCodeNotFound, ErrorCategory.Domain, $"Promo code '{code}' does not exist.", code);

    public static AppError InvalidState(string message, string state) =>
        new(ErrorCode.InvalidState, ErrorCategory.Domain, message, state);

    public static AppError FileNotFound(string path) =>
        new(ErrorCode.FileNotFound, ErrorCategory.Operational, "Data file was not found.", path);

    public static AppError MalformedContent(string path, string detail) =>
        new(ErrorCode.MalformedContent, ErrorCategory.Operational, $"Data file content is malformed: {detail}", path);

    public static AppError FileAccessError(string path, string detail) =>
        new(ErrorCode.FileAccessError, ErrorCategory.Operational, $"Data file could not be read or written: {detail}", path);

    public static AppError InvalidFeeInput(string detail) =>
        new(ErrorCode.InvalidFeeInput, ErrorCategory.Domain, detail, "ServiceFee");

    public static AppError InvalidReportParameters(string detail) =>
        new(ErrorCode.InvalidReportParameters, ErrorCategory.Domain, detail, "Report");

    public static AppError NativeLibraryUnavailable(string library, string detail) =>
        new(ErrorCode.NativeLibraryUnavailable, ErrorCategory.Operational, $"Native library could not be loaded: {detail}", library);

    public static AppError NativeBindingFailed(string function, string detail) =>
        new(ErrorCode.NativeBindingFailed, ErrorCategory.Operational, $"Native function could not be bound: {detail}", function);

    public static AppError NativeCallRejected(string function, long code) =>
        new(ErrorCode.NativeCallRejected, ErrorCategory.Operational, $"Native function returned error code {code}.", function);

    public static AppError BookingNotFound(string reference) =>
        new(ErrorCode.BookingNotFound, ErrorCategory.Domain, $"Booking '{reference}' was not found among the confirmed bookings.", reference);

    public static AppError InvalidCancellationDate(string reference) =>
        new(ErrorCode.InvalidCancellationDate, ErrorCategory.Domain, "The cancellation date is earlier than the booking date.", reference);

    public static AppError InvalidRefundPercent(int percent) =>
        new(ErrorCode.InvalidRefundPercent, ErrorCategory.Domain, $"The refund policy returned {percent} percent; it must be between 0 and 100.", "RefundPolicy");
}
