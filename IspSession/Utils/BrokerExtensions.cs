using System.Numerics;
using System.Reflection;

namespace NCV.ISPSession.Utils;

internal static class BrokerExtensions
{
    private static readonly Type[]  OtherSimpleTypes = [
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
        var nullableUnderlyingType = Nullable.GetUnderlyingType(type);
        var nullableUnderlyingTypeInfo = nullableUnderlyingType?.GetTypeInfo();

        if (nullableUnderlyingType != null && (nullableUnderlyingTypeInfo!.IsPrimitive || OtherSimpleTypes.Contains(nullableUnderlyingType) || nullableUnderlyingTypeInfo.IsEnum))
        {
            return true;
        }

        var typeInfo = type.GetTypeInfo();
        return typeInfo.IsPrimitive || OtherSimpleTypes.Contains(type) || typeInfo.IsEnum;
    }
}
