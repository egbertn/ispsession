using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCV.ISPSession.Utils;
using StackExchange.Redis;

namespace NCV.ISPSession.Internal;

/// <summary>
/// acts as a broker between redis and Session and Application State
/// </summary>
internal sealed class StateBroker
{
    private readonly IISPSessionConnectionMultiplexer _iSPSessionConnectionMultiplexer;
    private readonly ILogger<StateBroker> _logger;
    private readonly GlobalState _globalState;
    // initialized by reading cookie
    // note that because of scoped registration this works
    private string? _sessionId;
    private string? _correlationId;
    //we must not change IV logic to keep keys
    internal readonly static byte[] IV = Guid.Parse("5fcd556e-e92a-47ab-81cd-1972ba27c2bc").ToByteArray();

    internal StateBroker(
        IISPSessionConnectionMultiplexer databaseAsync,
        GlobalState globalState,
        ILogger<StateBroker> logger)
    {
        _iSPSessionConnectionMultiplexer = databaseAsync;
        _globalState = globalState;
        _logger = logger;
    }

    private Task<IDatabase> GetDatabase() => _iSPSessionConnectionMultiplexer.GetDatabaseAsync();
    public void RemoveSessionCookie(HttpContext httpContext)
    {
        if (_globalState.Affinity == AffinityMethods.Cookie)
        {
            httpContext.Response.Cookies.Delete(_globalState.SessionCookieName!, new CookieOptions
            {
                Secure = _globalState.SecureCookie,
                IsEssential = true,
                HttpOnly = true,
                SameSite =  _globalState.SameSiteMode,

            });
        }
    }
    /// <summary>
    /// checks if domain is like {*}.domain.com
    /// if so, will replace {*} with the current host
    /// </summary>
    /// <param name="cookieDomain">the cookieDomain like hostz.mydomain.com</param>
    /// <param name="host">actual running hostname like </param>
    /// <returns></returns>
    internal static string? ProcessWildCardDomain(string? cookieDomain, string host)
    {
        string wildCard = "{*}";
        if (cookieDomain == null || cookieDomain.Length == 0)
        {
            return default;
        }
        if (cookieDomain.StartsWith(wildCard) && host.Length > 3)
        {
            if (host.EndsWith(cookieDomain[3..]))
            {
                return host;
            }
        }
        return cookieDomain;
    }
    /// <summary>
    /// just take current session it and regenerate
    /// requirement, works only at existing session
    /// </summary>
    internal async Task RenewCookie(HttpContext context, TimeSpan dataExpiresAbsolute, TimeSpan? cookieExpires)
    {
        var request = context.Request;
        var response = context.Response;
        var cookies = response.Cookies;
        var now = DateTime.UtcNow;

        if (_globalState.Affinity is not AffinityMethods.Cookie)
        {
            throw new NotSupportedException("you only can renew a HTTP Cookie");
        }
        var database = await GetDatabase();

        var existingSessionHandle = request.Cookies.TryGetValue(_globalState.SessionCookieName!, out _sessionId);
        if (!existingSessionHandle)
        {
            throw new InvalidOperationException("No session cookie found");
        }
        //session could have been deleted or expired

        cookies.Delete(_globalState.SessionCookieName!);

        cookies.Append(_globalState.SessionCookieName!, _sessionId!,
            new CookieOptions
            {
                Secure = _globalState.SecureCookie,
                Domain = ProcessWildCardDomain(_globalState.CookieDomain, request.Host.Host),
                Path = _globalState.CookiePath,
                IsEssential = true,
                HttpOnly = true,
                SameSite = _globalState.SameSiteMode,
                Expires = cookieExpires != null ? now.Add(cookieExpires.Value) : null
            });

        cookies!.Delete(_globalState.CorrelationCookieName!);
        cookies!.Append(_globalState.CorrelationCookieName!, _correlationId,
            new CookieOptions
            {
                Domain = ProcessWildCardDomain(_globalState.CookieDomain, request.Host.Host),
                Path = _globalState.CookiePath,
                Secure = _globalState.SecureCookie,
                HttpOnly = false, //no risk involved, no correlation with backend
                SameSite = _globalState.SameSiteMode,
                Expires = cookieExpires != null ? now.Add(cookieExpires.Value) : null
            });
        var encryptedSessionKey = EncryptKey(_sessionId!);
        //marker for don't touch
        var longSessionKey = EncryptKey($"{_sessionId}_immortal");
        await database.StringSetAsync(longSessionKey, 1, dataExpiresAbsolute, flags: CommandFlags.FireAndForget);
        _ = await database.KeyExpireAsync(encryptedSessionKey, dataExpiresAbsolute, CommandFlags.FireAndForget);

    }
    bool _keyIsImmortal = false;

    internal async Task<(Stream?, string sessionId, bool isNew, bool isExpired, long version)>
        GetSessionAsync(HttpContext httpContext)
    {
        ValidateConfigurationThrowIfInvalid();
        var request = httpContext.Request;
        var response = httpContext.Response;


        Stream ret = null!;
        bool isNew = false;
        bool isExpired = false;
        //first check if there is a session cookie
        var existingSessionHandle = false;
        long _sessionStateVersion = 0L;
        if (_globalState.Affinity == AffinityMethods.Cookie)
        {
            existingSessionHandle = request.Cookies.TryGetValue(_globalState.SessionCookieName!, out _sessionId);
        }
        else if (_globalState.Affinity == AffinityMethods.IPAddress)
        {

            _sessionId = IPExtensions.IPHash(httpContext).ToString();
            existingSessionHandle = true; // IP-based: session id is always derivable
        }
        else if (_globalState.Affinity == AffinityMethods.CustomHeader)
        {
            if (request.Headers.TryGetValue(_globalState.SessionCookieName!, out var values))
            {
                if (existingSessionHandle = values.Count > 0)
                {
                    _sessionId = values[0];
                }
            }
        }
        else if (_globalState.Affinity == AffinityMethods.FormField)
        {
            if (request.Form.TryGetValue(_globalState.SessionCookieName!, out var values))
            {
                if (existingSessionHandle = values.Count > 0)
                {
                    _sessionId = values[0];
                }
            }
        }
        else if (_globalState.Affinity == AffinityMethods.CustomInit)
        {
            //we do Sessioninit later
            return (null, string.Empty, true, true, 0);
        }
        //assume we are new
        bool generateNewSession = true;
        var database = await GetDatabase();

        if (existingSessionHandle)
        {
            var encryptedSessionKey = EncryptKey(_sessionId!);
            var resultStateEncrypted = await database.StringGetAsync(encryptedSessionKey!);
            //session could have been deleted or expired
            if (!resultStateEncrypted.IsNullOrEmpty)
            {
                generateNewSession = false;
                ret = KeyCrypto.DecryptToStream(resultStateEncrypted!, _globalState.KeyEncryptionSecret);
            }
            else
            {
                isExpired = true;
            }
        }
        //ipaddress && FormField are readonly thus we cannot create a new SessionId
        if (generateNewSession && _globalState.Affinity != AffinityMethods.IPAddress && _globalState.Affinity != AffinityMethods.FormField)
        {
            //overwrite if any or create new
            _sessionId = Guid.NewGuid().ToString();
            isNew = true;
        }

        bool existingCorrelation = false;
        IResponseCookies? cookies = null;
        if (_globalState.Affinity == AffinityMethods.Cookie)
        {
            existingCorrelation = request.Cookies.TryGetValue(_globalState.CorrelationCookieName!, out _correlationId);

            cookies = response.Cookies;
            if (generateNewSession)
            {
                cookies.Delete(_globalState.SessionCookieName!);
                cookies.Append(_globalState.SessionCookieName!, _sessionId!,
                    new CookieOptions
                    {
                        Secure = _globalState.SecureCookie,
                        Domain = ProcessWildCardDomain(_globalState.CookieDomain, request.Host.Host),
                        Path = _globalState.CookiePath,
                        IsEssential = true,
                        HttpOnly = true,
                        SameSite = _globalState.SameSiteMode,
                        Expires = _globalState.Expires
                    });

            }
        }
        //optimistic concurrency protection
        // if two ore more read the session at the same time, only one can save
        var encVersion = EncryptKey($"{_sessionId}_ver");
        var immortalExistsKey = EncryptKey($"{_sessionId}_immortal");
        _keyIsImmortal = await database.KeyExistsAsync(immortalExistsKey);
        _sessionStateVersion = await database.StringIncrementAsync(encVersion);
        if (!_keyIsImmortal)
        {
            await database.KeyExpireAsync(encVersion, _globalState.DataTimeOut, CommandFlags.FireAndForget);
        }
        if (_globalState.Affinity == AffinityMethods.Cookie)
        {
            // in case of an existing correlation cookie
            // no need to swap, it is the same user/browser
            if (!existingCorrelation)
            {
                _correlationId = Guid.NewGuid().ToString();
                cookies!.Delete(_globalState.CorrelationCookieName!);
                cookies!.Append(_globalState.CorrelationCookieName!, _correlationId,
                    new CookieOptions
                    {
                        Domain = ProcessWildCardDomain(_globalState.CookieDomain, request.Host.Host),
                        Path = _globalState.CookiePath,
                        Secure = _globalState.SecureCookie,
                        HttpOnly = false, //no risk involved, no correlation with backend
                        SameSite = _globalState.SameSiteMode,
                        Expires = _globalState.Expires
                    });
            }
        }
        if (_globalState.Affinity == AffinityMethods.CustomHeader)
        {
            response.Headers.TryAdd(_globalState.SessionCookieName!, _sessionId);
        }
        return (ret, _sessionId!, isNew, isExpired, _sessionStateVersion);
    }

    internal async Task<(Stream?, bool isNew, bool isExpired, long version)> GetSessionLateAsync(string sessionId)
    {
        Stream ret = null!;
        _sessionId = sessionId;
        bool isNew = false;
        bool isExpired = false;
        //first check if there is a session cookie
        long _sessionStateVersion = 0L;
        if (_globalState.Affinity != AffinityMethods.CustomInit)
        {
            throw new InvalidOperationException("AfinityMethod must be CustomInit");

        }
        //assume we are new
        var database = await GetDatabase();

        var encryptedSessionKey = EncryptKey(_sessionId!);
        var resultStateEncrypted = await database.StringGetAsync(encryptedSessionKey!);
        //session could have been deleted or expired
        if (!resultStateEncrypted.IsNullOrEmpty)
        {
            ret = KeyCrypto.DecryptToStream(resultStateEncrypted!, _globalState.KeyEncryptionSecret);
        }
        else
        {
            isExpired = true;
            isNew = true;
        }

        //ipaddress && FormField are readonly thus we cannot create a new SessionId
        //optimistic concurrency protection
        // if two ore more read the session at the same time, only one can save
        var encVersion = EncryptKey($"{_sessionId}_ver");
        _sessionStateVersion = await database.StringIncrementAsync(encVersion);
        await database.KeyExpireAsync(encVersion, _globalState.DataTimeOut, CommandFlags.FireAndForget);

        return (ret, isNew, isExpired, _sessionStateVersion);
    }

    internal async Task GetApplicationAsync(IDictionary<string, KeyState> keys)
    {
        var appKey = _globalState.ApplicationName ??
            throw new InvalidOperationException("IspSession Section in appsettings ApplicationName should be specified");

        var appKeyEnc = EncryptKey(appKey);
        var database = await GetDatabase();
        var applicationKeys = await database.SetMembersAsync(appKeyEnc);
        var keyCount = applicationKeys.Length;
        RedisKey[] redisKeys = new RedisKey[keyCount];
        string[] unencryptedKeys = new string[keyCount];
        for (int x = 0; x < keyCount; x++)
        {
            //convert value to key
            redisKeys[x] = (byte[]?)applicationKeys[x];
            //key is in form {appkey}:{keyname} split it
            unencryptedKeys[x] = DecryptKey(redisKeys[x]).value![(appKey.Length + 1)..];
        }
        if (keyCount > 0)
        {
            RedisValue[] values = await database.StringGetAsync(redisKeys);
            for (int x = 0; x < keyCount; x++)
            {
               keys.Add(new
                (
                    unencryptedKeys[x], new KeyState(values[x].IsNullOrEmpty ? null : KeyCrypto.DecryptToBytes(values[x]!, _globalState.KeyEncryptionSecret))
                ));
            }
        }
    }

    internal async Task SaveApplication(ApplicationState appState)
    {
        var applicationName = _globalState.ApplicationName ?? throw new InvalidOperationException("ApplicationName Null");
        var dictionary = appState.Dictionary;

        List<KeyValuePair<string, KeyState>> newKeys = [];
        List<KeyValuePair<string, KeyState>> changedKeys = [];
        List<KeyValuePair<string, KeyState>> removedKeys = [];
        List<KeyValuePair<string, KeyState>> expiredKeys = [];
        foreach (var kvp in dictionary)
        {
            if (kvp.Value.IsNew) newKeys.Add(kvp);
            if (kvp.Value.Dirty) changedKeys.Add(kvp);
            if (kvp.Value.Remove) removedKeys.Add(kvp);
            if (kvp.Value.ExpirateAtUtc != null) expiredKeys.Add(kvp);
        }

        if (removedKeys.Count > 0 || changedKeys.Count > 0 || newKeys.Count > 0 || expiredKeys.Count > 0)
        {
            var setKey = EncryptKey(applicationName);
            var database = await GetDatabase();

            if (newKeys.Count > 0)
            {
                await database.SetAddAsync(setKey, [.. newKeys.Select(s => EncryptValue($"{applicationName}_{s.Key}"))], CommandFlags.FireAndForget);
            }

            if (removedKeys.Count > 0)
            {
                await database.SetRemoveAsync(setKey, [.. removedKeys.Select(s => EncryptValue($"{applicationName}_{s.Key}"))], CommandFlags.FireAndForget);
            }
            //KVP is a struct, therefore, values are copied by value, not by reference
            //make sure to use the least amount of mem footprint
            var multipleSet = new KeyValuePair<RedisKey, RedisValue>[newKeys.Count + changedKeys.Count];

            int ct = 0;
            using (MemoryStream stream = new())
            {
                foreach (var (k, v) in newKeys.Concat(changedKeys))
                {
                    var isEmpty = v.Value == null;
                    if ((v.IsNew || v.Dirty) && !isEmpty)
                    {
                        stream.SetLength(0);
                        stream.WriteValue(v.Value);
                        stream.Position = 0;
                    }
                    multipleSet[ct++] = new KeyValuePair<RedisKey, RedisValue>(EncryptKey($"{applicationName}_{k}"),
                        !isEmpty ? KeyCrypto.EncryptToBytes(stream, _globalState.KeyEncryptionSecret) : null);
                }
            }

            if (ct > 0)
            {
                await database.StringSetAsync(multipleSet, When.Always, CommandFlags.FireAndForget);
            }
            if (expiredKeys.Count > 0)
            {
                foreach (var (k, v) in expiredKeys)
                {
                    var result = await database.KeyExpireAsync(EncryptKey($"{applicationName}_{k}"), v.ExpirateAtUtc,
                        _globalState.SecureCookie? CommandFlags.None: CommandFlags.FireAndForget);
                    _logger.LogTrace("Exp {ExpirateAt} for {k}", v.ExpirateAtUtc, k);
                    if (result == false &&_globalState.SecureCookie)
                    {
                        _logger.LogWarning("Tried to expire key {k} but not found", k);
                    }
                }
            }
        }
        else
        {
            _logger.LogDebug("Nothing to do");
        }
    }
    internal async Task<bool> IsImmortalSessionAsync(IDatabaseAsync db) =>
        _sessionId != null && await db.KeyExistsAsync(EncryptKey($"{_sessionId}_immortal"));

    internal async Task SaveSession(SessionState sessionState)
    {
        RedisKey encryptedSessionKey = new();
        var database = await GetDatabase();

        if (_sessionId != null)
        {
            var (blob, isDirty) = sessionState.ToBlob();

            encryptedSessionKey = EncryptKey(_sessionId!);

            if (sessionState.AbandonSession)
            {
                _ = await database.KeyDeleteAsync(EncryptKey(_sessionId!), CommandFlags.FireAndForget);
            }
            else
            {
                if (isDirty)
                {
                    long versionCheck = await database.StringIncrementAsync(EncryptKey($"{_sessionId}_ver"), 0L);
                    if (sessionState.version != versionCheck)
                    {
                        _logger.LogWarning("{sessionId} Concurrency Conflict", _sessionId);
                        return;
                    }
                    var ttl = _keyIsImmortal ? (TimeSpan?)null : _globalState.DataTimeOut;

                    _ = await database.StringSetAsync(
                        encryptedSessionKey,
                        KeyCrypto.EncryptToBytes(blob, _globalState.KeyEncryptionSecret),
                        expiry: ttl,
                        keepTtl: _keyIsImmortal,
                        flags: CommandFlags.FireAndForget
                    );

                }
                else if (!_keyIsImmortal)
                {
                    _ = await database.KeyExpireAsync(encryptedSessionKey, _globalState.DataTimeOut, CommandFlags.FireAndForget);
                }
            }
            blob.Dispose();

        }
        if (_correlationId != null && _sessionId != null)
        {
            RedisKey correlationKey = EncryptKey(_correlationId!.ToString());
            _ = await database.StringSetAsync(correlationKey, (byte[])encryptedSessionKey!, _globalState.DataTimeOut, flags: CommandFlags.FireAndForget);
        }
    }


    // returns a sessionstate in a static-ish way, only to be called from
    // analysis prospective
    internal async Task<Stream> GetSessionFromGuidAsync(Guid sessionId, bool dontThrow = false)
    {
        if (sessionId.Equals(Guid.Empty))
        {
            throw new ArgumentOutOfRangeException(nameof(sessionId), "cannot use empty");
        }

        RedisKey sessionKey = EncryptKey(sessionId.ToString());
        var database = await GetDatabase();
        var resultState = await database.StringGetAsync(sessionKey!);

        if (resultState.IsNullOrEmpty)
        {
            if (dontThrow == false)
            {
                throw new InvalidOperationException($"Tried to get SessionId {sessionId} but not found");
            }
            return new MemoryStream((byte[])resultState!);
        }

        return KeyCrypto.DecryptToStream(resultState!, _globalState.KeyEncryptionSecret);
    }


    // Using an existing correlationId (guid) will try to retrieve the corresponding sessiondId
    // if the session does not exist, will return null
    public async Task<Guid?> GetSessionIdFromCorrelationIdAsync(Guid correlationId)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(correlationId));
        }
        ValidateConfigurationThrowIfInvalid();

        RedisKey correlationKey = EncryptKey(correlationId.ToString()!);
        var database = await GetDatabase();
        RedisValue sessionKey = await database.StringGetAsync(correlationKey);
        if (sessionKey.IsNullOrEmpty)
        {
            return null;
        }

        var (result, value) = DecryptKey((RedisKey)(byte[])sessionKey!);
        return result ? new Guid(value!) : null;
    }


    internal RedisKey EncryptKey(string value) => KeyCrypto.Encrypt(value, _globalState.KeyEncryptionSecret, IV).ToArray();
    internal RedisValue EncryptValue(string value) => KeyCrypto.Encrypt(value, _globalState.KeyEncryptionSecret, IV).ToArray();

    internal (bool success, string? value) DecryptKey(RedisKey key) => KeyCrypto.Decrypt((byte[])key!, _globalState.KeyEncryptionSecret, IV);

    internal RedisKey GetMonitoringSessionKey() => EncryptKey(_globalState.MonitorSessionKey);

    private void ValidateConfigurationThrowIfInvalid()
    {
        if (_globalState.Affinity == AffinityMethods.Cookie)
        {
            if (string.IsNullOrEmpty(_globalState.SessionCookieName))
            {
                throw new InvalidOperationException($"Tried to use {nameof(StateBroker)} but IspSession:{nameof(_globalState.SessionCookieName)} but it is not set. this is required when Affinity is set to Cookie (default).");
            }
            if (string.IsNullOrEmpty(_globalState.CorrelationCookieName))
            {
                throw new InvalidOperationException($"Tried to use {nameof(StateBroker)} but IspSession:{nameof(_globalState.CorrelationCookieName)} but it is not set. this is required when Affinity is set to Cookie (default).");
            }
        }
        if (string.IsNullOrEmpty(_globalState.KeyEncryptionSecret))
        {
            throw new InvalidOperationException($"Tried to use {nameof(StateBroker)} but IspSession:{nameof(_globalState.KeyEncryptionSecret)} but it is not set. this is required to have session keys decrypted. See Redis section in appsettings.");
        }
    }
}