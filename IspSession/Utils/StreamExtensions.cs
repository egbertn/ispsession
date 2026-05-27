using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using NCV.ISPSession.Internal;
using TypeCode = NCV.ISPSession.Internal.TypeCode;

namespace NCV.ISPSession.Utils;

internal static class StreamExtensions
{
    private const int MAX_STACK_SIZE = 2000;

    private static readonly JsonSerializerOptions DefaultOptions = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition =  System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    internal static void WriteValue(this Stream memoryStream, object? value)
    {
        int pos = 0;

        pos = (int)memoryStream.Position;
        memoryStream.WriteInt32(0); //temporary, the length should come here
        TypeCode typeCode = TypeCode.Empty;
        if (value == null)
        {
            memoryStream.WriteByte((byte)typeCode); //empty
        }
        else
        {
            Type type = value.GetType();

            typeCode = type.IsSimpleType() ? (TypeCode)(int)Type.GetTypeCode(type) :TypeCode.Object;
            //.net does not map this
            if (typeCode == TypeCode.Object)
            {
                if (type == typeof(DateTimeOffset))
                    typeCode = TypeCode.DateTimeOffset;
                else if (type == typeof(Guid))
                    typeCode = TypeCode.Guid;
                else if (type == typeof(TimeSpan))
                    typeCode = TypeCode.TimeSpan;
                else if (type == typeof(DateOnly))
                    typeCode = TypeCode.DateOnly;
                else if (type == typeof(TimeOnly))
                    typeCode = TypeCode.TimeOnly;
                else if (type == typeof(BigInteger))
                    typeCode = TypeCode.BigInteger;
            }
            memoryStream.WriteByte((byte)typeCode);

            switch (typeCode)
            {
                case TypeCode.Object:
                    var utfBuffer = JsonSerializer.SerializeToUtf8Bytes(value, DefaultOptions);
                    memoryStream.Write(utfBuffer);
                    break;
                case TypeCode.Int32:
                    memoryStream.WriteInt32((int)value);
                    break;
                case TypeCode.String:
                    memoryStream.WriteLengthPrefixedUtfString((string)value);
                    break;
                case TypeCode.Boolean:
                    memoryStream.WriteBoolean((bool)value);
                    break;
                case TypeCode.Byte:
                    memoryStream.WriteByte((byte)value);
                    break;
                case TypeCode.Int16:
                    memoryStream.WriteInt16((short)value);
                    break;
                case TypeCode.Int64:
                    memoryStream.WriteInt64((long)value);
                    break;
                case TypeCode.Single:
                    memoryStream.WriteSingle((float)value);
                    break;
                case TypeCode.Double:
                    memoryStream.WriteDouble((double)value);
                    break;
                case TypeCode.Decimal:
                    memoryStream.WriteDecimal((decimal)value);
                    break;
                case TypeCode.DateTime:
                    memoryStream.WriteDateTime((DateTime)value);
                    break;
                case TypeCode.DateTimeOffset:
                    memoryStream.WriteDateTimeOffset((DateTimeOffset)value);
                    break;
                case TypeCode.Guid:
                    memoryStream.WriteGuid((Guid)value);
                    break;
                case TypeCode.TimeSpan:
                    memoryStream.WriteTimeSpan((TimeSpan)value);
                    break;
                case TypeCode.DateOnly:
                    memoryStream.WriteDateOnly((DateOnly)value);
                    break;
                case TypeCode.TimeOnly:
                    memoryStream.WriteTimeOnly((TimeOnly)value);
                    break;
                case TypeCode.BigInteger:
                    memoryStream.WriteBigInteger((BigInteger)value);
                    break;
                default:
                    throw new NotSupportedException($"typeCode {typeCode} {type} not supported");
            }
        }

        var newPos = (int)memoryStream.Position;
        int length = newPos - pos;
        memoryStream.Position = pos;
        memoryStream.WriteInt32(length);
        memoryStream.Position = newPos;
        //Console.WriteLine($"origpos: {pos}, TypeCode: {typeCode}, Length: {length}");

    }

    internal static T ReadValue<T>(this Stream memoryStream)
    {
        int valueLength = memoryStream.ReadInt32();
        TypeCode typeCode = (TypeCode)memoryStream.ReadByte();
        object? value;
        switch (typeCode)
        {
            case TypeCode.Empty:
                value = default;
                break;
            case TypeCode.Boolean:
                value = memoryStream.ReadBoolean();
                break;
            case TypeCode.Byte:
                value = (byte)memoryStream.ReadByte();
                break;
            case TypeCode.Int16:
                value = memoryStream.ReadInt16();
                break;
            case TypeCode.Int32:
                value = memoryStream.ReadInt32();
                break;
            case TypeCode.Int64:
                value = memoryStream.ReadInt64();
                break;
            case TypeCode.Single:
                value = memoryStream.ReadSingle();
                break;
            case TypeCode.Double:
                value = memoryStream.ReadDouble();
                break;
            case TypeCode.Decimal:
                value = memoryStream.ReadDecimal();
                break;
            case TypeCode.DateTime:
                value = memoryStream.ReadDateTime();
                break;
            case TypeCode.String:
                value = memoryStream.ReadLengthPrefixedUtfString();
                break;
            case TypeCode.DateTimeOffset:
                value = memoryStream.ReadDateTimeOffset();
                break;
            case TypeCode.Guid:
                value = memoryStream.ReadGuid();
                break;
            case TypeCode.TimeSpan:
                value = memoryStream.ReadTimeSpan();
                break;
            case TypeCode.DateOnly:
                value = memoryStream.ReadDateOnly();
                break;
            case TypeCode.TimeOnly:
                value = memoryStream.ReadTimeOnly();
                break;
            case TypeCode.BigInteger:
                value = memoryStream.ReadBigInteger();
                break;
            case TypeCode.Object:
                if (typeof(T) == typeof(object))
                {
                    throw new InvalidOperationException(
                        "Cannot deserialize a complex value via the non-generic Get(string) or Get<object>(string). Use Get<T>(string) with a concrete type instead.");
                }
                var shared = ArrayPool<byte>.Shared;
                byte[]? heapBytes = null;
                var jsonLength = valueLength - sizeof(byte) - sizeof(int);
                try
                {
                    Span<byte> bytes = jsonLength < MAX_STACK_SIZE ? stackalloc byte[jsonLength] : (heapBytes = shared.Rent(jsonLength));
                    memoryStream.Read(bytes[..jsonLength]);
                    value = JsonSerializer.Deserialize<T>(bytes[..jsonLength], DefaultOptions);

                }
                finally
                {
                    if (jsonLength >= MAX_STACK_SIZE)
                    {
                        shared.Return(heapBytes!);
                    }
                }
                break;
            default:
                throw new NotSupportedException($"typeCode {typeCode} not supported");
        }
        if (value == null) // Check of T een nullable referentietype is
        {
            // Als T een class is, en je wilt null ondersteunen, moet je een aparte logica of methode gebruiken
            // omdat deze implementatie T als een struct behandelt.
            return default!;
        }

        return (T)value;
    }

    internal static void ReadKeyValuePairs(this Stream stream, IDictionary<string, KeyState> keyValuePairs, int count)
    {
        keyValuePairs.Clear();
        for (int x = count; x != 0; x--)
        {
            var key = stream.ReadLengthPrefixedUtfString();
            int valueLen = stream.ReadInt32();
            byte[] buff = new byte[valueLen];
            stream.Position -= sizeof(int); //correct the prefetch
            stream.ReadExactly(buff, 0, valueLen);
            keyValuePairs[key] = new KeyState(buff);
        }
    }

    internal static void WriteBoolean(this Stream stream, bool value) => stream.WriteByte(value ? (byte)1 : (byte)0);

    internal static void WriteInt16(this Stream stream, short value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    internal static void WriteInt32(this Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    internal static void WriteInt64(this Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    internal static void WriteSingle(this Stream stream, float value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(float)];
        BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
        stream.Write(bytes);
    }

    internal static void WriteDouble(this Stream stream, double value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(double)];
        BinaryPrimitives.WriteDoubleLittleEndian(bytes, value);
        stream.Write(bytes);
    }

    internal static void WriteDecimal(this Stream stream, decimal value)
    {
        Span<int> bits = stackalloc int[sizeof(decimal) / sizeof(int)];
        decimal.TryGetBits(value, bits, out  _);
        Span<byte> partBytes = stackalloc byte[4];
        foreach (int part in bits)
        {
            BinaryPrimitives.WriteInt32LittleEndian(partBytes, part);
            stream.Write(partBytes);
        }
    }

    internal static void WriteDateTime(this Stream stream, DateTime value) => WriteInt64(stream, value.ToBinary());

    internal static void WriteDateTimeOffset(this Stream stream, DateTimeOffset value)
    {
        WriteInt64(stream, value.Ticks);
        WriteInt16(stream, (short)value.Offset.TotalMinutes);
    }

    internal static void WriteLengthPrefixedUtfString(this Stream stream, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            stream.WriteInt32(0);
            return;
        }

        var stringLength = Encoding.UTF8.GetByteCount(value);
        var totalLength = checked(stringLength + sizeof(int));
        var shared = ArrayPool<byte>.Shared;
        byte[]? heapBytes = null;
        try
        {
            Span<byte> buffer = totalLength <= MAX_STACK_SIZE ? stackalloc byte[totalLength] : (heapBytes = shared.Rent(totalLength));
            BinaryPrimitives.WriteInt32LittleEndian(buffer, stringLength);
            Encoding.UTF8.GetBytes(value, buffer[sizeof(int)..totalLength]);
            stream.Write(buffer[..totalLength]);
        }
        finally
        {
            if (heapBytes != null)
            {
                shared.Return(heapBytes);
            }
        }
    }

    internal static void WriteTimeSpan(this Stream stream, TimeSpan value) => WriteInt64(stream, value.Ticks);

    internal static void WriteDateOnly(this Stream stream, DateOnly value)
    {
        WriteInt16(stream, (short)value.Year);
        stream.WriteByte((byte)value.Month);
        stream.WriteByte((byte)value.Day);
    }
    internal static void WriteTimeOnly(this Stream stream, TimeOnly value) => WriteInt64(stream, value.Ticks);

    internal static void WriteGuid(this Stream stream, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes);
        stream.Write(bytes);
    }

    // writes 3x4 bytes being Major, Minor, Build (e.g. 1.0.1)
    internal static void WriteVersion(this Stream stream, Version version)
    {
        stream.WriteInt32(version.Major);
        stream.WriteInt32(version.Minor);
        stream.WriteInt32(version.Build);
    }

    internal static void WriteBigInteger(this Stream stream, BigInteger bigInteger)
    {
        var bigIntLength = bigInteger.GetByteCount();
        if (bigIntLength > MAX_STACK_SIZE)
        {
            throw new InvalidOperationException($"Actual length of BigInteger is {bigIntLength} we support only max {MAX_STACK_SIZE}");
        }
        Span<byte> bytes = stackalloc byte[bigIntLength];
        bigInteger.TryWriteBytes(bytes, out _, isBigEndian: !BitConverter.IsLittleEndian);
        stream.WriteInt32(bigIntLength);
        stream.Write(bytes);
    }

    internal static bool ReadBoolean(this Stream stream) => stream.ReadByte() == 1;

    internal static short ReadInt16(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt16LittleEndian(buffer);
    }

    internal static int ReadInt32(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    internal static long ReadInt64(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    internal static float ReadSingle(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(float)];
        stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadSingleLittleEndian(buffer);
    }

    internal static double ReadDouble(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[sizeof(double)];
        stream.ReadExactly(buffer);

        return BinaryPrimitives.ReadDoubleLittleEndian(buffer);
    }

    internal static decimal ReadDecimal(this Stream stream)
    {
        Span<int> parts = stackalloc int[4];
        Span<byte> buffer = MemoryMarshal.AsBytes(parts);
        stream.ReadExactly(buffer);
        return new decimal(parts);
    }

    internal static DateTime ReadDateTime(this Stream stream) => DateTime.FromBinary(stream.ReadInt64());

    internal static DateTimeOffset ReadDateTimeOffset(this Stream stream) => new(ReadInt64(stream), TimeSpan.FromMinutes(ReadInt16(stream)));

    internal static string ReadLengthPrefixedUtfString(this Stream stream)
    {
        var stringLength = stream.ReadInt32();
        if (stringLength < 0)
        {
            throw new InvalidDataException("String length cannot be negative.");
        }

        if (stringLength == 0)
        {
            return string.Empty;
        }

        byte[]? heapBytes = null;
        var shared = ArrayPool<byte>.Shared;
        Span<byte> buffer = stringLength <= MAX_STACK_SIZE ? stackalloc byte[stringLength] : (heapBytes = shared.Rent(stringLength));

        try
        {
            stream.ReadExactly(buffer[..stringLength]);
            return Encoding.UTF8.GetString(buffer[..stringLength]);
        }
        finally
        {
            if (heapBytes != null)
            {
                shared.Return(heapBytes);
            }
        }
    }

    internal static TimeSpan ReadTimeSpan(this Stream stream) => new(ReadInt64(stream));

    internal static DateOnly ReadDateOnly(this Stream stream) => new(stream.ReadInt16(), stream.ReadByte(), stream.ReadByte());

    internal static TimeOnly ReadTimeOnly(this Stream stream) => new(stream.ReadInt64());

    internal static Guid ReadGuid(this Stream stream)
    {
        Span<byte> buffer = stackalloc byte[16];
        stream.ReadExactly(buffer);
        return new Guid(buffer);
    }

    internal static Version ReadVersion(this Stream stream) => new(stream.ReadInt32(), stream.ReadInt32(), stream.ReadInt32());

    internal static BigInteger ReadBigInteger(this Stream stream)
    {
        var bigintBytes = stream.ReadInt32();
        Span<byte> buffer = stackalloc byte[bigintBytes];
        stream.ReadExactly(buffer);
        return new BigInteger(buffer, isBigEndian: !BitConverter.IsLittleEndian);
    }
}