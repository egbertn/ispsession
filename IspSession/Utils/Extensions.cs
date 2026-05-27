using System.Net;
using Microsoft.AspNetCore.Http;
using System.Numerics;
namespace NCV.ISPSession.Utils;

internal static class IPExtensions
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
    // <see href="https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Forwarded-For"/>
    private static readonly string XForwardedForHeader = "X-Forwarded-For";

    // combine all addresses between us and client
    private static IEnumerable<IPAddress> GetRemoteAddresses(HttpContext httpContext)
    {
        var testProxy = httpContext.Request.Headers.TryGetValue(XForwardedForHeader, out var xForwardedFor);

        if (testProxy)
        {
            foreach (var forwardedFor in xForwardedFor)
            {
                var success = IPAddress.TryParse(forwardedFor, out IPAddress? ipAddress);
                if (success)
                {
                    yield return ipAddress!;
                }
            }
        }
        else
        {
            var remoteIp = httpContext.Connection.RemoteIpAddress;

            if (remoteIp != null)
            {
                yield return remoteIp;
            }
        }
    }

    internal static int IPHash(HttpContext context)
    {
        //1-5 hops is max says gpt, worst-case 8x IPv6=128
        Span<byte> span = stackalloc byte[128];
        int offset = 0;
        foreach (var ip in GetRemoteAddresses(context))
        {
            ip.TryWriteBytes(span[offset..], out int written);
            offset += written;
        }
        var key = span[..offset].GetStableHashCode();
        return key;
    }

    internal static int GetStableHashCode(this Span<byte> data)
    {   //FNV-1a algorithm
        unchecked
        {
            const int p = 16777619;
            int hash = (int)2166136261;
            var l = data.Length;
            for (int i = 0; i < l; i++)
                hash = (hash ^ data[i]) * p;

            hash += hash << 13;
            hash ^= hash >> 7;
            hash += hash << 3;
            hash ^= hash >> 17;
            hash += hash << 5;
            return hash;
        }
    }


    public static bool IsSimpleType(this Type type)
    {
        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        return targetType.IsPrimitive || targetType.IsEnum || OtherSimpleTypes.Contains(targetType);
    }
}