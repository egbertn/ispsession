using System.Numerics;
using Microsoft.Extensions.Primitives;
using NCV.ISPSession.Internal;
using NCV.ISPSession.Utils;
namespace NCV.ISPSession.Tests;

public class ISPSessionStateExtensionsTests
{

    [Fact]
    public void IsSimpleTypeTest()
    {
        BigInteger bigInteger = new(100);
        Assert.True(bigInteger.GetType().IsSimpleType());
    }
    [Fact]
    public void WriteAndReadBigObjectTest()
    {
        using var stream = new MemoryStream();
        var originalValue = new TestObject { Id = 1, Name = new string('0', 2000) };

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<TestObject>();
        Assert.NotNull(readValue);
        Assert.Equal(originalValue.Id, readValue.Id);
        Assert.Equal(originalValue.Name, readValue.Name);
    }

    [Fact]
    public void WriteAndReadObjectTest()
    {
        using var stream = new MemoryStream();
        var originalValue = new TestObject { Id = 1, Name = "Test" };

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<TestObject>();
        Assert.NotNull(readValue);
        Assert.Equal(originalValue.Id, readValue.Id);
        Assert.Equal(originalValue.Name, readValue.Name);
    }

    [Fact]
    public void WriteAndReadBooleanTest()
    {
        using var stream = new MemoryStream();
        bool originalValue = true;

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<bool>();
        Assert.Equal(originalValue, readValue);
    }






    public class CounterResponse
    {
        public int Counter { get; set; }
    }

    [Fact]
    public void WriteAndReadByteTest()
    {
        using var stream = new MemoryStream();
        byte originalValue = 255;

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<byte>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadInt16Test()
    {
        using var stream = new MemoryStream();
        short originalValue = 32767;

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<short>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadInt64Test()
    {
        using var stream = new MemoryStream();
        long originalValue = 9223372036854775807;

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<long>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadInt64ArrayTest()
    {
        using var stream = new MemoryStream();
        long originalValue1 = 9223372036854775807;
        long originalValue2= long.MinValue;
        var array = new[] { originalValue1, originalValue2 };
        stream.WriteValue(array);
        stream.Position = 0;

        var readValue = stream.ReadValue<long[]>();

        Assert.Equal(array, readValue);
    }

    [Fact]
    public void WriteAndReadTimeSpanTest()
    {
        using var stream = new MemoryStream();
        TimeSpan originalValue = TimeSpan.FromHours(1);

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<TimeSpan>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadInt32Test()
    {
        using var stream = new MemoryStream();
        var originalValue = 12345;

        stream.WriteValue(originalValue);
        stream.Position = 0; // Reset stream position to the beginning before reading

        var readValue = stream.ReadValue<int>();

        Assert.Equal(originalValue, readValue);
    }

    [Theory]
    [InlineData("{*}.domain.com", "guid.domain.com", "guid.domain.com")]
    [InlineData(".domain.com", "guid.domain.com", ".domain.com")]
    [InlineData(null, "guid.domain.com", null)]
    public void CanProcesWildCarCOokieDomain(string? domain, string host, string? expected)
    {
        var result = StateBroker.ProcessWildCardDomain(domain, host);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void WriteAndReadEmptyObjectTest()
    {
        using var stream = new MemoryStream();
        object? originalValue = null;

        stream.WriteValue(originalValue);
        stream.Position = 0; // Reset stream position to the beginning before reading

        var readValue = stream.ReadValue<object?>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadNullStringTest()
    {
        using var stream = new MemoryStream();
        string? originalValue = null;

        stream.WriteValue(originalValue);
        stream.Position = 0; // Reset stream position to the beginning before reading

        var readValue = stream.ReadValue<string?>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadBigStringTest()
    {
        using var stream = new MemoryStream();
        var hw = "Hello, World!";
        var originalValue = new string('0', 2000) + hw;

        stream.WriteValue(originalValue);
        stream.Position = 0; // Reset stream position to the beginning before reading

        var readValue = stream.ReadValue<string>();

        Assert.Equal(2000+hw.Length, readValue.Length);
        Assert.EndsWith(hw, readValue);
    }

    [Fact]
    public void WriteAndReadStringTest()
    {
        using var stream = new MemoryStream();
        var originalValue = "Hello, World!";

        stream.WriteValue(originalValue);
        stream.Position = 0; // Reset stream position to the beginning before reading

        var readValue = stream.ReadValue<string>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadDateTimeTest()
    {
        using var stream = new MemoryStream();
        var originalValue = DateTime.UtcNow;

        stream.WriteValue(originalValue);
        stream.Position = 0; // Reset stream position to the beginning before reading

        var readValue = stream.ReadValue<DateTime>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadDateTimeOffsetTest()
    {
        using var stream = new MemoryStream();
        var originalValue = DateTimeOffset.UtcNow;

        stream.WriteValue(originalValue);
        stream.Position = 0; // Reset stream position to the beginning before reading

        var readValue = stream.ReadValue<DateTimeOffset>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadBigIntegerTest()
    {
        using var stream = new MemoryStream();
        var originalValue = new BigInteger(int.MaxValue) * 2;

        stream.WriteValue(originalValue);
        stream.Position = 0; // Reset stream position to the beginning before reading

        var readValue = stream.ReadValue<BigInteger>();

        Assert.Equal(originalValue, readValue);
    }



    [Fact]
    public void WriteAndReadGuidTest()
    {
        using var stream = new MemoryStream();
        var originalValue = Guid.NewGuid();

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<Guid>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadDateOnlyTest()
    {
        using var stream = new MemoryStream();
        var originalValue = new DateOnly(2022, 12, 31);

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<DateOnly>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadTimeOnlyTest()
    {
        using var stream = new MemoryStream();
        var originalValue = new TimeOnly(23, 59, 59);

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<TimeOnly>();

        Assert.Equal(originalValue, readValue);
    }

    // Voortzetting van ISPSessionStateExtensionsTests

    [Fact]
    public void WriteAndReadSingleTest()
    {
        using var stream = new MemoryStream();
        var originalValue = 3.14f;

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = (float)stream.ReadValue<float>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadDoubleTest()
    {
        using var stream = new MemoryStream();
        var originalValue = 3.14159;

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<double>();

        Assert.Equal(originalValue, readValue);
    }

    [Fact]
    public void WriteAndReadDecimalTest()
    {
        using var stream = new MemoryStream();
        var originalValue = 123.456m;

        stream.WriteValue(originalValue);
        stream.Position = 0;

        var readValue = stream.ReadValue<decimal>();

        Assert.Equal(originalValue, readValue);
    }
}