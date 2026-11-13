using System.Globalization;
using TicketBookingSystem.Domain;
using TicketBookingSystem.Infrastructure;
using Xunit;

namespace TicketBookingSystem.Tests;

public class NativeIntegrationTests
{
    private static decimal Money(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);

    private static Func<long, int, int, int, long> Throwing(Exception exception) =>
        (_, _, _, _) => throw exception;

    [Fact]
    public void RealCall_StandardSchedule_ReturnsManuallyCalculatedFee()
    {
        var adapter = new NativeFeeAdapter(FeeSchedule.Standard);

        var fee = Support.ExpectSuccess(adapter.CalculateFeeCents(270.00m, 2));

        Assert.Equal(775L, fee);
    }

    [Theory]
    [InlineData("182.00", 4, 655L)]
    [InlineData("750.00", 5, 2125L)]
    [InlineData("455.00", 10, 1638L)]
    [InlineData("135.00", 1, 388L)]
    [InlineData("450.00", 3, 1275L)]
    public void RealCall_BookingExamples_MatchManualValues(string amount, int quantity, long expected)
    {
        var adapter = new NativeFeeAdapter(FeeSchedule.Standard);

        var fee = Support.ExpectSuccess(adapter.CalculateFeeCents(Money(amount), quantity));

        Assert.Equal(expected, fee);
    }

    [Fact]
    public void RealCall_HalfCentRounding_RoundsUp()
    {
        var adapter = new NativeFeeAdapter(new FeeSchedule(250, 0));

        var fee = Support.ExpectSuccess(adapter.CalculateFeeCents(0.20m, 1));

        Assert.Equal(1L, fee);
    }

    [Fact]
    public void RealCall_ZeroAmount_ReturnsOnlyPerTicketFee()
    {
        var adapter = new NativeFeeAdapter(FeeSchedule.Standard);

        var fee = Support.ExpectSuccess(adapter.CalculateFeeCents(0m, 3));

        Assert.Equal(150L, fee);
    }

    [Fact]
    public void RealCall_MaximumAmount_ReturnsExpectedFee()
    {
        var adapter = new NativeFeeAdapter(FeeSchedule.Standard);

        var fee = Support.ExpectSuccess(adapter.CalculateFeeCents(10_000_000_000.00m, 1));

        Assert.Equal(25_000_000_050L, fee);
    }

    [Theory]
    [InlineData("-0.01", 1)]
    [InlineData("10000000000.01", 1)]
    [InlineData("10.00", 0)]
    [InlineData("10.00", 1000001)]
    public void InvalidInputs_AreRejectedBeforeNativeCall(string amount, int quantity)
    {
        var nativeCalls = 0;
        var adapter = new NativeFeeAdapter(FeeSchedule.Standard, (_, _, _, _) =>
        {
            nativeCalls++;
            return 0;
        });

        var error = Support.ExpectFailure(adapter.CalculateFeeCents(Money(amount), quantity));

        Assert.Equal(ErrorCode.InvalidFeeInput, error.Code);
        Assert.Equal(ErrorCategory.Domain, error.Category);
        Assert.Equal(0, nativeCalls);
    }

    [Fact]
    public void InvalidSchedule_IsRejectedBeforeNativeCall()
    {
        var nativeCalls = 0;
        var adapter = new NativeFeeAdapter(new FeeSchedule(10001, 50), (_, _, _, _) =>
        {
            nativeCalls++;
            return 0;
        });

        var error = Support.ExpectFailure(adapter.CalculateFeeCents(10m, 1));

        Assert.Equal(ErrorCode.InvalidFeeInput, error.Code);
        Assert.Equal(0, nativeCalls);
    }

    [Fact]
    public void LibraryNotFound_BecomesNativeLibraryUnavailable()
    {
        var adapter = new NativeFeeAdapter(FeeSchedule.Standard, Throwing(new DllNotFoundException("ticketfees missing")));

        var error = Support.ExpectFailure(adapter.CalculateFeeCents(10m, 1));

        Assert.Equal(ErrorCode.NativeLibraryUnavailable, error.Code);
        Assert.Equal(ErrorCategory.Operational, error.Category);
        Assert.Equal("ticketfees", error.Subject);
    }

    [Fact]
    public void WrongArchitecture_BecomesNativeLibraryUnavailable()
    {
        var adapter = new NativeFeeAdapter(FeeSchedule.Standard, Throwing(new BadImageFormatException("wrong architecture")));

        var error = Support.ExpectFailure(adapter.CalculateFeeCents(10m, 1));

        Assert.Equal(ErrorCode.NativeLibraryUnavailable, error.Code);
    }

    [Fact]
    public void MissingExport_BecomesNativeBindingFailed()
    {
        var adapter = new NativeFeeAdapter(FeeSchedule.Standard, Throwing(new EntryPointNotFoundException("no such export")));

        var error = Support.ExpectFailure(adapter.CalculateFeeCents(10m, 1));

        Assert.Equal(ErrorCode.NativeBindingFailed, error.Code);
        Assert.Equal("tf_service_fee_cents", error.Subject);
    }

    [Fact]
    public void NativeErrorCode_BecomesNativeCallRejected()
    {
        var adapter = new NativeFeeAdapter(FeeSchedule.Standard, (_, _, _, _) => -1L);

        var error = Support.ExpectFailure(adapter.CalculateFeeCents(10m, 1));

        Assert.Equal(ErrorCode.NativeCallRejected, error.Code);
        Assert.Contains("-1", error.Message);
    }
}