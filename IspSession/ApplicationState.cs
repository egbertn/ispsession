using System.Collections;
using NCV.ISPSession.Internal;
using NCV.ISPSession.Utils;

namespace NCV.ISPSession;

/// <summary>
/// This class enables you sophisticated and performant caching of application-level data.
/// Implement a background service using <see cref="KeyExpiredEventHook"/> to subscribe to expiration events.
/// </summary>
public interface IApplicationState : IAsyncDisposable, IEnumerable<string>
{

    /// <summary>
    /// returns the count of keys that exist in this Application State
    /// </summary>
    int Count { get; }

    /// <summary>
    /// retrieves the value for specified key
    /// </summary>
    /// <typeparam name="T"></typeparam>
    T? Get<T>(string key);


    /// <summary>
    /// Sets the specified key with value
    /// </summary>
    /// <param name="key">any key name</param>
    /// <param name="value">an object instance or type.<br/>
    /// Note that to objects apply the serialisation rules that exist for <see cref="System.Text.Json.JsonSerializer"/> ></param>
    /// <param name="ttl">Optionally specify expiration for this key</param>
    void Set(string key, object value, TimeSpan? ttl = default);
    /// <summary>
    /// marks the specified key for removal when the request goes out of scope
    /// </summary>
    void RemoveKey(string key);

    /// <summary>
    /// marks all application keys for removal when the request goes out of scope
    /// </summary>
    void Clear();

    /// <summary>
    /// Marks the key for expiration when the request goes out of scope
    /// Redis will effectively take care of it
    /// </summary>
    /// <param name="key"></param>
    /// <param name="at">the timespan given relative to UTC</param>
    void ExpireKeyAt(string key, TimeSpan at);
}

/// <summary>
/// Unbound from <see cref="Microsoft.AspNetCore.Http.HttpContext"/>
/// Use e.g. in a HostedService and wrap within an AsyncScope
/// </summary>
public interface IApplicationStateUnbound : IAsyncDisposable, IEnumerable<string>
{

    /// <summary>
    /// Makes sure that state is initialized once within your scope
    /// </summary>
    Task EnsureInitializedAsync();

    /// <summary>
    /// returns the count of keys that exist in this Application State
    /// </summary>
    int Count { get; }

    /// <summary>
    /// retrieves the value for specified key
    /// </summary>
    /// <typeparam name="T"></typeparam>
    T? Get<T>(string key);

    /// <summary>
    /// Sets the specified key with value
    /// </summary>
    /// <param name="key">any key name</param>
    /// <param name="value">an object instance or type.<br/>
    /// Note that to objects apply the serialisation rules that exist for <see cref="System.Text.Json.JsonSerializer"/> ></param>
    /// <param name="ttl">Optionally specify expiration for this key</param>
    void Set(string key, object value, TimeSpan? ttl = default);
    /// <summary>
    /// marks the specified key for removal when the request goes out of scope
    /// </summary>
    void RemoveKey(string key);

    /// <summary>
    /// marks all application keys for removal when the request goes out of scope
    /// </summary>
    void Clear();

    /// <summary>
    /// Marks the key for expiration when the request goes out of scope
    /// Redis will effectively take care of it
    /// </summary>
    /// <param name="key"></param>
    /// <param name="at">the timespan given relative to UTC</param>
    void ExpireKeyAt(string key, TimeSpan at);
}


internal sealed class ApplicationState: IApplicationState, IApplicationStateUnbound
{
    private readonly Dictionary<string, KeyState> _keyValuePairs = [];

    private readonly StateBroker _sessionBroker;
    public int Count => _keyValuePairs.Count(c => !c.Value.Remove);

    internal ApplicationState(StateBroker sessionBroker)
    {
        _sessionBroker = sessionBroker;
    }
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            await InitAsync();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
    internal async Task<bool> InitAsync()
    {
        await _sessionBroker.GetApplicationAsync(_keyValuePairs);
        return true;
    }

    /// <summary>
    /// returns a readonly copy of the dictionary
    /// note that name is kept the same to keep some consistancy
    /// </summary>
    internal IReadOnlyDictionary<string, KeyState> Dictionary => _keyValuePairs;

    ValueTask IAsyncDisposable.DisposeAsync() => new (_sessionBroker.SaveApplication(this));

    IEnumerator<string> IEnumerable<string>.GetEnumerator() => _keyValuePairs.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _keyValuePairs.Keys.GetEnumerator();

    public T? Get<T>(string key)
    {
        if(_keyValuePairs.TryGetValue(key, out KeyState? keyState))
        {
            if (keyState.Value != null)
            {
                return (T)keyState.Value;
            }
            if (keyState.State == null)
            {
                return default;
            }

            using MemoryStream memoryStream = new(keyState.State);
            // we need to compensate the not written length which does exist in ISessionState
            var value = memoryStream.ReadValue<T>(_sessionBroker.JsonContext);
            keyState.Value = value;
            return value;
        }
        return default;
    }

    public void Set(string key, object value, TimeSpan? ttl)
    {
        var keyExists = _keyValuePairs.TryGetValue(key, out var keyState);
        if (keyExists == false)
        {
            keyState = new KeyState(null) { Value = value, IsNew = true };
            if (ttl != null)
            {
                keyState.ExpirateAtUtc = DateTime.UtcNow.Add(ttl.Value);
            }
            _keyValuePairs.Add(key, keyState);
        }
        else
        {
            if (!keyState!.IsNew)
            {
                keyState.Dirty = true;
            }
            keyState.Value = value;
        }
    }

    public void RemoveKey(string key)
    {
        if (_keyValuePairs.TryGetValue(key, out var keyState))
        {
            keyState.Remove = true;
        }
    }

    public void Clear()
    {
        foreach(var (_, v) in _keyValuePairs)
        {
            v.Remove = true;
        }
    }

    public void ExpireKeyAt(string key, TimeSpan at)
    {
        if (_keyValuePairs.TryGetValue(key, out var keyState))
        {
            keyState.ExpirateAtUtc = DateTime.UtcNow.Add(at);
        }
    }
}
