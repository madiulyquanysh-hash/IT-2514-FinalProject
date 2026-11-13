using System.Globalization;
using TicketBookingSystem.Domain;

namespace TicketBookingSystem.Infrastructure;

public sealed class NativeFeeAdapter : IFeeCalculator
{
    private const string LibraryName = "ticketfees";
    private const string FunctionName = "tf_service_fee_cents";

    private readonly FeeSchedule schedule;
    private readonly Func<long, int, int, int, long> nativeCall;

    public NativeFeeAdapter(FeeSchedule schedule)
        : this(schedule, NativeMethods.ServiceFeeCents)
    {
    }

    public NativeFeeAdapter(FeeSchedule schedule, Func<long, int, int, int, long> nativeCall)
    {
        this.schedule = schedule;
        this.nativeCall = nativeCall;
    }

    public Result<long, AppError> CalculateFeeCents(decimal amount, int quantity) =>
        ToValidatedCents(amount, quantity)
            .Bind(amountCents => Invoke(amountCents, quantity));

    private Result<long, AppError> ToValidatedCents(decimal amount, int quantity)
    {
        var maxAmount = FeeLimits.MaxAmountCents / 100m;

        if (amount < 0m || amount > maxAmount)
        {
            return Result<long, AppError>.Fail(AppError.InvalidFeeInput(
                $"Amount must be between 0.00 and {maxAmount.ToString("F2", CultureInfo.InvariantCulture)}."));
        }

        if (quantity < 1 || quantity > FeeLimits.MaxQuantity)
        {
            return Result<long, AppError>.Fail(AppError.InvalidFeeInput(
                $"Quantity must be between 1 and {FeeLimits.MaxQuantity}."));
        }

        if (schedule.RateBasisPoints < 0 || schedule.RateBasisPoints > FeeLimits.MaxRateBasisPoints)
        {
            return Result<long, AppError>.Fail(AppError.InvalidFeeInput(
                $"Rate must be between 0 and {FeeLimits.MaxRateBasisPoints} basis points."));
        }

        if (schedule.PerTicketCents < 0 || schedule.PerTicketCents > FeeLimits.MaxPerTicketCents)
        {
            return Result<long, AppError>.Fail(AppError.InvalidFeeInput(
                $"Per-ticket fee must be between 0 and {FeeLimits.MaxPerTicketCents} cents."));
        }

        var cents = decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
        return Result<long, AppError>.Ok(cents);
    }

    private Result<long, AppError> Invoke(long amountCents, int quantity)
    {
        try
        {
            var fee = nativeCall(amountCents, quantity, schedule.RateBasisPoints, schedule.PerTicketCents);

            return fee < 0
                ? Result<long, AppError>.Fail(AppError.NativeCallRejected(FunctionName, fee))
                : Result<long, AppError>.Ok(fee);
        }
        catch (DllNotFoundException ex)
        {
            return Result<long, AppError>.Fail(AppError.NativeLibraryUnavailable(LibraryName, ex.Message));
        }
        catch (BadImageFormatException ex)
        {
            return Result<long, AppError>.Fail(AppError.NativeLibraryUnavailable(LibraryName, ex.Message));
        }
        catch (EntryPointNotFoundException ex)
        {
            return Result<long, AppError>.Fail(AppError.NativeBindingFailed(FunctionName, ex.Message));
        }
    }
}