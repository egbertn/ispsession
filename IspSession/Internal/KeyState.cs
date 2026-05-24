namespace NCV.ISPSession.Internal;

internal sealed class KeyState
{
    internal KeyState(byte[]? state)
    {
        State = state;
    }

    internal byte[]? State { get; }

    private bool _dirty;

    internal bool Dirty
    {
        get => _dirty;

        set
        {
            if (_remove)
            {
                throw new InvalidOperationException("this key is already marked for removal");
            }
            _dirty = value;
        }
    }


    // will return true if the key never was stored at rest

    internal bool IsNew { get; set; }

    private bool _remove;

    // when true, will not be saved at the end of the scope
    internal bool Remove
    {
        get => _remove;
        set
        {
            if (_dirty && value == true)
            {
                _dirty = false;
            }
            _remove = value;
        }
    }

    // <summary>
    // if specified, will make this key be ignored after specified interval
    // Effectively the key may remain in Redis but at the moment after the interval
    /// note:utc
    internal DateTime? ExpirateAtUtc { get; set; }

    /// <summary>
    /// value if deserialized on a JIT basis
    /// </summary>
    internal object? Value { get; set; }
}