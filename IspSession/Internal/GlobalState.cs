using Microsoft.AspNetCore.Http;

namespace NCV.ISPSession.Internal;

internal sealed class GlobalState
{
    private readonly Version _blobVersion = Version.Parse("1.0.0");

    /// <summary>
    /// Internal use
    /// </summary>
    internal GlobalState(ISPSessionOptions options, ISPSessionRuntimeOptions runtimeOptions)
    {
        CorrelationCookieName = runtimeOptions.CorrellationCookieName;
        SessionCookieName = runtimeOptions.SessionCookieName;
        KeyEncryptionSecret = options.KeyEncryptionSecret;
        CookieDomain = runtimeOptions.CookieDomain;
        CookiePath = runtimeOptions.CookiePath;
        DataTimeOut = runtimeOptions.DataTimeOut;
        ApplicationName = runtimeOptions.ApplicationName;
        MonitorSessionKey = options.MonitorSessionKey ?? "00000000-0000-0000-0000-000000000001";
        SubscribeExpireEvents = runtimeOptions.SubscribeExpireEvents;
        Affinity = runtimeOptions.AffinityMethod;
        Compressed = runtimeOptions.CompressData;
        SameSiteMode = runtimeOptions.SameSite;
        Mode = runtimeOptions.Mode;
        SecureCookie = runtimeOptions.CookieSecure;
        Expires = runtimeOptions.Expires != null ? DateTimeOffset.UtcNow + runtimeOptions.Expires : null;
    }

    public AffinityMethods Affinity { get; }

    public bool SecureCookie {get;}

    public Version BlobVersion => _blobVersion;

    public string? CorrelationCookieName { get; }

    public string? SessionCookieName { get; }

    public TimeSpan DataTimeOut { get; }

    public string KeyEncryptionSecret { get; }

    public bool Compressed { get; }

    public string? ApplicationName { get; }

    public string? CookieDomain { get;}

    public string? CookiePath { get;  }

    public SameSiteMode SameSiteMode { get; }

    public DateTimeOffset? Expires {get; }

    public string MonitorSessionKey { get; }

    public bool SubscribeExpireEvents {get;}

    public UseMode Mode { get; }
}