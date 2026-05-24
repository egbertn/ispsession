using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace NCV.ISPSession.Internal;

internal class ISPSessionConnectionMultiplexer : IISPSessionConnectionMultiplexer
{
    private readonly Lazy<Task<ConnectionMultiplexer>> _connectionMultiplexerAsync;
    private readonly int defaultDb;
    private readonly ConfigurationOptions _configurationOptions;

    public ISPSessionConnectionMultiplexer(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IspSession") ??  throw new InvalidOperationException("Missing ConnectionStrings:IspSession in appsettings");
        _configurationOptions = ConfigurationOptions.Parse(connectionString);
        defaultDb = _configurationOptions.DefaultDatabase ?? -1;
        _connectionMultiplexerAsync = new Lazy<Task<ConnectionMultiplexer>>(() => ConnectionMultiplexer.ConnectAsync(_configurationOptions));
    }

    public async Task<IDatabase> GetDatabaseAsync()
    {
        var connection = await _connectionMultiplexerAsync.Value;
        return connection.GetDatabase(defaultDb);
    }

    public async Task<IConnectionMultiplexer> GetConnectionMultiplexerAsync(bool allowAdmin)
    {
        _configurationOptions.AllowAdmin = allowAdmin;
        return await _connectionMultiplexerAsync.Value;
    }
}