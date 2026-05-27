using System.Numerics;

namespace NCV.ISPSession.Utils;

internal static class BrokerExtensions
{
    private static readonly HashSet<Type>  OtherSimpleTypes = [
            typeof(string),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(DateOnly),
            typeof(TimeOnly),
            typeof(decimal),
            typeof(float),
            typeof(double),
            typeof(BigInteger) ];

    public static bool IsSimpleType(this Type type)
    {
        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        return targetType.IsPrimitive || targetType.IsEnum || OtherSimpleTypes.Contains(targetType);
    }
}
