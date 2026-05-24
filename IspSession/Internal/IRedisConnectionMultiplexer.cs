using StackExchange.Redis;

namespace NCV.ISPSession.Internal;

/// <summary>
/// Used to isolate ISP Session Redis communication
/// Do not use, for internal usage only
/// </summary>
public interface IISPSessionConnectionMultiplexer
{
    /// <summary>
    /// internal use
    /// </summary>
    public Task<IDatabase> GetDatabaseAsync();

    /// <summary>
    /// internal use
    /// </summary>
    public Task<IConnectionMultiplexer> GetConnectionMultiplexerAsync(bool allowAdmin = false);
}