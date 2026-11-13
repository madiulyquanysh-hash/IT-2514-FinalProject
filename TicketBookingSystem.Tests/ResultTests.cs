using TicketBookingSystem.Domain;
using Xunit;

namespace TicketBookingSystem.Tests;

public class ResultTests
{
    private static readonly AppError SampleError = AppError.InvalidQuantity("Sample Event");

    [Fact]
    public void Map_Success_TransformsValue()
    {
        var result = Result<int, AppError>.Ok(5).Map(x => x * 2);

        var success = Assert.IsType<Result<int, AppError>.Success>(result);
        Assert.Equal(10, success.Value);
    }

    [Fact]
    public void Bind_Success_PreservesNextResult()
    {
        var received = 0;

        var result = Result<int, AppError>.Ok(5).Bind(x =>
        {
            received = x;
            return Result<string, AppError>.Ok("value-" + x);
        });

        Assert.Equal(5, received);
        var success = Assert.IsType<Result<string, AppError>.Success>(result);
        Assert.Equal("value-5", success.Value);
    }

    [Fact]
    public void Bind_Success_PropagatesFailureReturnedByNextOperation()
    {
        var result = Result<int, AppError>.Ok(5).Bind(_ => Result<string, AppError>.Fail(SampleError));

        var failure = Assert.IsType<Result<string, AppError>.Failure>(result);
        Assert.Equal(SampleError, failure.Error);
    }

    [Fact]
    public void Map_Failure_PreservesFailureAndSkipsFunction()
    {
        var calls = 0;
        var failed = Result<int, AppError>.Fail(SampleError);

        var result = failed.Map(x =>
        {
            calls++;
            return x + 1;
        });

        Assert.Equal(0, calls);
        var failure = Assert.IsType<Result<int, AppError>.Failure>(result);
        Assert.Equal(SampleError, failure.Error);
    }

    [Fact]
    public void Bind_Failure_PreservesFailureAndSkipsFunction()
    {
        var calls = 0;
        var failed = Result<int, AppError>.Fail(SampleError);

        var result = failed.Bind(x =>
        {
            calls++;
            return Result<int, AppError>.Ok(x + 1);
        });

        Assert.Equal(0, calls);
        var failure = Assert.IsType<Result<int, AppError>.Failure>(result);
        Assert.Equal(SampleError, failure.Error);
    }

    [Fact]
    public void Success_WithNullPayload_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string, AppError>.Ok(null!));
    }

    [Fact]
    public void Failure_WithNullError_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string, AppError>.Fail(null!));
    }
}