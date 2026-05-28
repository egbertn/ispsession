using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace NCV.ISPSession;

/// <summary>
/// Concerns Isp Session runtime settings that are typically
/// compile time only
/// </summary>
public sealed class ISPSessionRuntimeOptions
{
    /// <summary>
    /// defaults to 10 minutes
    /// The longer you set this, the more storage will be required especially when you
    /// facilitate thousands of sessions
    /// </summary>
    public TimeSpan DataTimeOut { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Defaults to "IspSessionCorrelation".
    /// The name of the correlation for the browser session client
    /// </summary>
    public string CorrellationCookieName { get; set; } = "IspSessionCorrelation";

    /// <summary>
    /// defaults to 'IspSession'.
    /// The name of the cookie for the session.
    /// We will set this cookie as Essential, Session, HttpOnly and Strict
    /// </summary>
    public string SessionCookieName { get; set; } = "IspSession";

    /// <summary>
    /// optional domain of the session cookie, e.g. ".mydomain.com"
    /// </summary>
    public string? CookieDomain { get; set; }

    /// <summary>
    /// optional Path of the session cookie, e.g. "/"
    /// </summary>
    public string? CookiePath { get; set;}

    /// <summary>
    /// Specifies the Session Cookie SameSite mode
    /// Defaults to strict
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;

    /// <summary>
    /// defines the Secureness of the sessioncookie, whether or not the cookie must work only over https or http(s)
    /// When in production or staging this value will be set to true
    /// ISP Session does not allow unsecure cookies in production
    /// </summary>
    public bool CookieSecure { get; set;}

    /// <summary>
    /// defines relatively the persistance expiration value, if not used, it is a session only cookie.
    /// Use with care
    /// </summary>
    public TimeSpan? Expires { get; set;}

    /// <summary>
    /// Defaults to using Session cookies.
    /// Specifies which affinity strategy ISP Sessions need to follow
    /// Note that setting it to IPAddress assumes involves the risk
    /// that different people or devices theoretically could have the same IP address
    /// with the session timespan
    /// </summary>
    public AffinityMethods AffinityMethod { get; set; } = AffinityMethods.Cookie;

    /// <summary>
    /// Defaults to true. Stores all data as compressed.
    /// to save redis memory
    /// Set to false if you never want compression
    /// It is a good idea to see if your use case
    /// makes sense in terms of data storage and costs.
    /// </summary>
    public bool CompressData { get; set; } = true;

    /// <summary>
    /// Defaults to "Default"
    /// This is the name under which you uniqely identify your asp.net core application
    /// If you have more services make sure that each service has it's own application name
    /// Used to prefix keys
    /// </summary>
    public string? ApplicationName { get; set; } = "Default";

    /// <summary>
    /// Default Both. Configures which MiddleWare modules will be loaded
    /// Application State
    /// Session State
    /// </summary>
    public UseMode Mode { get; set; } = UseMode.Both;

        /// <summary>
    /// defaults to true. When set to false, will not attempt to
    /// subscribe to Redis for receiving key expired events
    /// Theoretically, you could enable one node running ISP Session to true
    /// to avoid duplicate events on different machines
    /// </summary>
    public bool SubscribeExpireEvents { get; set; } = true;

}

/// <summary>
/// options used for ISP Session State and ApplicationState
/// </summary>
public sealed class ISPSessionOptions
{

    /// <summary>
    /// the password used for encrypting/decrypting keys
    /// </summary>
    public string KeyEncryptionSecret { get; set; } = null!;

    /// <summary>
    /// a valid Guid which is used a system-use only session
    /// keep this secret
    /// </summary>
    public string? MonitorSessionKey { get; set; }

    /// <summary>
    /// Optional. Provide a <see cref="JsonSerializerContext"/> to enable AOT-safe JSON
    /// serialization of complex objects stored in session/application state.
    /// When omitted, reflection-based serialization is used (compatible with JIT, not AOT-safe).
    /// </summary>
    public JsonSerializerContext? JsonContext { get; set; }

}
