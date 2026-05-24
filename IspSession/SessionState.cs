using System.Collections;
using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using NCV.ISPSession.Internal;
using NCV.ISPSession.Utils;

namespace NCV.ISPSession;

/// <summary>
/// Enables session affinity maintenance through cookies, IP, a FormField, or a custom header.
/// For application-level smart caching, refer to <see cref="IApplicationState"/>.
/// </summary>
public interface ISessionState : IAsyncDisposable, IEnumerable<string>
{
    /// <summary>
    /// Returns the number of keys stored in the session.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// True if the session is new and a correlation cookie has been set.
    /// </summary>
    bool IsNew { get; }

    /// <summary>
    /// True if the cookie was found but no record was found in storage.
    /// </summary>
    bool IsExpired { get; }

    /// <summary>
    /// Returns the actual sessionId used.
    /// </summary>
    string? SessionId { get; }

    /// <summary>
    /// Retrieves value by specified key.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    T? Get<T>(string key);

    /// <summary>
    /// Sets the value for the given key.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value">An object instance or type.
    /// Note that to objects apply the serialization rules that exist for <see cref="System.Text.Json.JsonSerializer"/>.
    /// </param>
    void Set(string key, object value);

    /// <summary>
    /// Marks a key to be removed when saving this session.
    /// </summary>
    void RemoveKey(string key);

    /// <summary>
    /// Marks all keys to be removed from the session.
    /// </summary>
    void Clear();

    /// <summary>
    /// Deletes the session stored data associated with the configured Affinity.
    /// If Affinity is set to Cookie (default), the cookie will be deleted too.
    /// </summary>
    void Abandon(HttpContext context);

    /// <summary>
    /// degrade or upgrade a session cookie timeout
    /// Does not affect other modes
    /// </summary>
    /// <param name="context">The active HttpContext</param>
    /// <param name="DataTimeOut">e.g. 20 minutes</param>
    /// <param name="expires">preferibly the same as DataTimeout</param>
    Task RenewCookie(HttpContext context, TimeSpan DataTimeOut, TimeSpan? expires);
}


internal sealed class SessionState : ISessionState
{
    private readonly Dictionary<string, KeyState> _keyValuePairs = [];
    private readonly GlobalState _globalState;
    private readonly StateBroker _sessionBroker;

    internal SessionState(GlobalState globalState, StateBroker sessionBroker)
    {
        _globalState = globalState;
        _sessionBroker = sessionBroker;
    }

    public int Count => _keyValuePairs.Count(w => !w.Value.Remove);

    public bool IsNew { get; internal set; }

    public bool IsExpired { get; internal set; }

    public string? SessionId { get; internal set; }

    internal long version;
    internal async Task InitAsync(HttpContext context)
    {
        (var memoryStream, SessionId, IsNew, IsExpired, version) = await _sessionBroker.GetSessionAsync(context);
        if (memoryStream == Stream.Null)
        {
            return;
        }
        if (!IsNew && !IsExpired)
        {
            //can never be null at this point
            LoadState(memoryStream!);
            memoryStream!.Dispose();
        }
    }

    internal async Task<bool> InitLateAsync(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        (var memoryStream,  IsNew, IsExpired, version) = await _sessionBroker.GetSessionLateAsync(sessionId);
        SessionId = sessionId;
        if (memoryStream == Stream.Null)
        {
            return false;
        }
        if (!IsNew && !IsExpired)
        {
            //can never be null at this point
            LoadState(memoryStream!);
            memoryStream!.Dispose();
        }
        return true;
    }
    private void LoadState(Stream memoryStream)
    {
        ArgumentNullException.ThrowIfNull(memoryStream, nameof(memoryStream));
        if(memoryStream.Length == 0)
        {
            throw new InvalidOperationException("MemoryStream cannot be zero length");
        }
        Version version = memoryStream.ReadVersion();
        if (version != _globalState.BlobVersion)
        {
            //TODO make logic to define which versions would break
            throw new InvalidOperationException($"Tried to read incompatible versions {version} expected {_globalState.BlobVersion}");
        }
        bool useDecompress = memoryStream.ReadBoolean();
        var count = memoryStream.ReadInt16();
        if (useDecompress)
        {
            // If we need to decompress, use a BrotliStream to decompress, and then copy the decompressed content to a MemoryStream.
            using var brotliStream = new BrotliStream(memoryStream, CompressionMode.Decompress, true);
            using var stream = new MemoryStream();
            brotliStream.CopyTo(stream);
            stream.Position = 0;
            stream.ReadKeyValuePairs(_keyValuePairs, count);
        }
        else
        {
            memoryStream.ReadKeyValuePairs(_keyValuePairs, count);
        }
    }


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
            var value = memoryStream.ReadValue<T>();
            keyState.Value = value;
            return value;

        }
        return default;
    }

    public void Set(string key, object value)
    {
        var keyExists = _keyValuePairs.TryGetValue(key, out var keyState);
        if (keyExists == false)
        {
            keyState = new KeyState(null) { Value = value, IsNew = true };
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


    // Serializes to a blob
    internal (Stream blob, bool dirty) ToBlob()
    {   // do not use using, this is returnValue
        MemoryStream outputStream = new();

        short count = (short)_keyValuePairs.Count;
        var version = _globalState.BlobVersion;
        //first 12+1+4 are uncompressed
        outputStream.WriteVersion(version);
        var compressedPos = outputStream.Position;

        outputStream.WriteBoolean(false);
        outputStream.WriteInt16(count);
        var uncompressedLength = (int)outputStream.Position;

        bool dirty = false;

        foreach(var (k,v) in _keyValuePairs)
        {
            outputStream.WriteLengthPrefixedUtfString(k);
            if (v.Dirty || v.IsNew)
            {
                dirty = true;
                outputStream.WriteValue(v.Value);
            }
            else
            {
                if (v.State == null)
                {
                    outputStream.WriteInt32(0);
                }
                else
                {
                    outputStream.Write(v.State);
                }
            }
        }
        bool compress = outputStream.Length > 1024 && _globalState.Compressed ;
        //version + compression and count flag are stored uncompresed
        if (compress)
        {
            // no disposal, compressed is return variable
            MemoryStream compressed = new((int)outputStream.Length);
            Span<byte> head = stackalloc byte[uncompressedLength];
            outputStream.Position = compressedPos;
            outputStream.WriteBoolean(true);
            outputStream.Position = 0;
            outputStream.Read(head);
            compressed.Write(head);
            BrotliStream brotliStream = new(compressed, CompressionLevel.Optimal, true);
            outputStream.CopyTo(brotliStream);
            brotliStream.Dispose();
            compressed.Position = 0;
            return (compressed, dirty);
        }

        outputStream.Position = 0;
        return (outputStream, dirty);
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await _sessionBroker.SaveSession(this);

    IEnumerator<string> IEnumerable<string>.GetEnumerator() => _keyValuePairs.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _keyValuePairs.Keys.GetEnumerator();

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

    internal bool AbandonSession;

    public async Task RenewCookie(HttpContext context, TimeSpan dataTimeout, TimeSpan? expires)
    {
        await _sessionBroker.RenewCookie(context, dataTimeout, expires);
    }

    public void Abandon(HttpContext context)
    {
        AbandonSession = true;
        _sessionBroker.RemoveSessionCookie(context);
        _keyValuePairs.Clear();
    }
}