using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;

namespace NCV.ISPSession.Utils;

internal static class IPExtensions
{
    // <see href="https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Forwarded-For"/>
    private static readonly string XForwardedForHeader = new(['X', '-', 'F', 'o', 'r', 'w', 'a', 'r', 'd', 'e', 'd', '-', 'F', 'o', 'r']);
    private const string XForwardedForHost = "X-Forwarded-Host";
    private const string XRealIp = "X-Real-Ip";

    internal static bool GetRealIp(this HttpRequest request, out IPAddress? iPAddress)
    {
        iPAddress = default;
        if (request.Headers.TryGetValue(XRealIp, out var values) == false)
        {
            return false;
        }
        if (IPAddress.TryParse(values[0], out IPAddress? address))
        {
            iPAddress = address;
        }
        return true;
    }

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

    internal static bool IsLocalNetwork(this IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }
        else if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            Span<byte> bytes = stackalloc byte[4];
            ip.TryWriteBytes(bytes, out _);

            return (bytes[0] == 10) ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Check for Unique Local Address (ULA) fc00::/7
            if (ip.IsIPv6UniqueLocal || ip.IsIPv6LinkLocal)
            {
                return true;
            }

        }
        return false;
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
}