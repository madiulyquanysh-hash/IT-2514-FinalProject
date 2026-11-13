using TicketBookingSystem.Domain;
using TicketBookingSystem.Infrastructure;
using Xunit;

namespace TicketBookingSystem.Tests;

public sealed class CatalogLoaderTests : IDisposable
{
    private const string ValidJson = """
    {
      "events": [
        { "name": "Tech Conference", "unitPrice": 150.0, "availableCapacity": 50 }
      ],
      "promoCodes": [
        { "code": "WELCOME10", "discountPercent": 10 },
        { "code": "NODISCOUNT", "discountPercent": 0 }
      ]
    }
    """;

    private const string OtherValidJson = """
    {
      "events": [
        { "name": "Jazz Night", "unitPrice": 45.5, "availableCapacity": 10 },
        { "name": "Opera", "unitPrice": 80.0, "availableCapacity": 20 }
      ],
      "promoCodes": []
    }
    """;

    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "ticket-tests-" + Guid.NewGuid().ToString("N"));

    public CatalogLoaderTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static T ExpectSuccess<T>(Result<T, AppError> result) =>
        Assert.IsType<Result<T, AppError>.Success>(result).Value;

    private static AppError ExpectFailure<T>(Result<T, AppError> result) =>
        Assert.IsType<Result<T, AppError>.Failure>(result).Error;

    [Fact]
    public void Load_ValidFile_ReturnsCatalogs()
    {
        var path = WriteFile("valid.json", ValidJson);

        var data = ExpectSuccess(CatalogLoader.Load(path));

        Assert.Equal(1, data.Events.Count);
        Assert.Equal(2, data.Promos.Count);
        Assert.IsType<Maybe<EventInfo>.Some>(data.Events.Find("Tech Conference"));
        Assert.Equal(0, Assert.IsType<Maybe<int>.Some>(data.Promos.Find("NODISCOUNT")).Value);
    }

    [Fact]
    public void Load_MissingFile_ReturnsFileNotFound()
    {
        var path = Path.Combine(directory, "missing.json");

        var error = ExpectFailure(CatalogLoader.Load(path));

        Assert.Equal(ErrorCode.FileNotFound, error.Code);
        Assert.Equal(ErrorCategory.Operational, error.Category);
        Assert.Equal(path, error.Subject);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Load_MissingDirectory_ReturnsFileNotFound()
    {
        var path = Path.Combine(directory, "no-such-folder", "catalog.json");

        var error = ExpectFailure(CatalogLoader.Load(path));

        Assert.Equal(ErrorCode.FileNotFound, error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{ "events": [ { "name": "A", "unitPrice": 10.0, """)]
    [InlineData("""{ "promoCodes": [] }""")]
    [InlineData("""{ "events": [] }""")]
    [InlineData("""{ "events": [ null ], "promoCodes": [] }""")]
    [InlineData("""{ "events": [ { "name": "A", "unitPrice": "abc", "availableCapacity": 1 } ], "promoCodes": [] }""")]
    [InlineData("""{ "events": [ { "name": "A", "unitPrice": -5, "availableCapacity": 1 } ], "promoCodes": [] }""")]
    [InlineData("""{ "events": [ { "name": "A", "unitPrice": 5 } ], "promoCodes": [] }""")]
    [InlineData("""{ "events": [ { "name": "A", "unitPrice": 5, "availableCapacity": 1 }, { "name": "a", "unitPrice": 6, "availableCapacity": 2 } ], "promoCodes": [] }""")]
    [InlineData("""{ "events": [], "promoCodes": [ { "code": "X", "discountPercent": 150 } ] }""")]
    [InlineData("""{ "events": [], "promoCodes": [ { "code": "X" } ] }""")]
    public void Load_MalformedContent_ReturnsMalformedContent(string content)
    {
        var path = WriteFile("bad.json", content);

        var error = ExpectFailure(CatalogLoader.Load(path));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Equal(ErrorCategory.Operational, error.Category);
        Assert.Equal(path, error.Subject);
    }

    [Fact]
    public void Store_Open_MissingFile_ReturnsFailure()
    {
        var error = ExpectFailure(BookingDataStore.Open(Path.Combine(directory, "missing.json")));

        Assert.Equal(ErrorCode.FileNotFound, error.Code);
    }

    [Fact]
    public void Store_ReloadMissingFile_PreservesCurrentData()
    {
        var store = ExpectSuccess(BookingDataStore.Open(WriteFile("valid.json", ValidJson)));
        var before = store.Current;

        var error = ExpectFailure(store.Reload(Path.Combine(directory, "missing.json")));

        Assert.Equal(ErrorCode.FileNotFound, error.Code);
        Assert.Same(before, store.Current);
        Assert.Equal(1, store.Current.Events.Count);
    }

    [Fact]
    public void Store_ReloadMalformedFile_PreservesCurrentData()
    {
        var store = ExpectSuccess(BookingDataStore.Open(WriteFile("valid.json", ValidJson)));
        var before = store.Current;
        var badPath = WriteFile("bad.json", "{ \"events\": [ ");

        var error = ExpectFailure(store.Reload(badPath));

        Assert.Equal(ErrorCode.MalformedContent, error.Code);
        Assert.Same(before, store.Current);
        Assert.Equal(2, store.Current.Promos.Count);
    }

    [Fact]
    public void Store_ReloadValidFile_ReplacesCurrentData()
    {
        var store = ExpectSuccess(BookingDataStore.Open(WriteFile("valid.json", ValidJson)));
        var before = store.Current;

        var reloaded = ExpectSuccess(store.Reload(WriteFile("other.json", OtherValidJson)));

        Assert.NotSame(before, store.Current);
        Assert.Same(reloaded, store.Current);
        Assert.Equal(2, store.Current.Events.Count);
        Assert.Equal(0, store.Current.Promos.Count);
    }
}