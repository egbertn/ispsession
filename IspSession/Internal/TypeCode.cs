namespace NCV.ISPSession.Internal;

//copied from .net plus extra types
internal enum TypeCode
{
    Empty = 0,
    Object = 1,
    Boolean = 3,
    Byte = 6,
    Int16 = 7,
    Int32 = 9,
    Int64 = 11,
    Single = 13,
    Double = 14,
    Decimal = 15,
    DateTime = 16,
    String = 18,
    DateTimeOffset = 19,
    Guid = 20,
    DateOnly = 21,
    TimeOnly = 22,
    TimeSpan = 23,
    BigInteger = 24,
    Unknown = int.MaxValue
}
