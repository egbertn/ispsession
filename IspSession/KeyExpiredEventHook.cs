using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCV.ISPSession.Internal;
using StackExchange.Redis;

namespace NCV.ISPSession;

/// <summary>
/// Enables subscription to application-level key expiration events when implemented in a class that extends <see cref="Microsoft.Extensions.Hosting.IHostedService"/> or <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>.
/// Ensure "notify-keyspace-events Ex" is configured in redis.conf for functionality. ISPSession attempts to auto-configure this for Redis (tested with Redis 7.x) otherwise SubscribeExpireEvents is disabled.
/// </summary>
public sealed class KeyExpiredEventHook
{
    private readonly IISPSessionConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<KeyExpiredEventHook> _logger;
    private readonly IServiceProvider _serviceProvider;
    private string? _applicationName;

    internal KeyExpiredEventHook
    (
        IISPSessionConnectionMultiplexer iSPSessionConnectionMultiplexer,
        ILogger<KeyExpiredEventHook> logger,
        IServiceProvider serviceProvider
    )
    {
        _connectionMultiplexer = iSPSessionConnectionMultiplexer;
        _logger = logger;
        _subscriber = null;
        _serviceProvider = serviceProvider;
    }

    // Definieer een delegate voor het event
    /// <summary>
    /// Call back signature that you can implement in your application
    /// to which ISP Session will push notifications when a key has been
    /// expired by Redis
    /// </summary>
    /// <param name="key">the key which expired</param>
    /// <param name="applicationState">The application state</param>
    public delegate Task KeyExpiredEventHandlerAsync(string key, IApplicationState applicationState);

    /// <summary>
    /// the event you need to implement
    /// </summary>
    public event KeyExpiredEventHandlerAsync? KeyExpiredAsync;

    private RedisChannel _channelName;

    private ISubscriber? _subscriber;

    private async Task OnKeyExpiredAsync(string key, IApplicationState applicationState)
    {
        if (KeyExpiredAsync != null)
        {
            var invocationList = KeyExpiredAsync.GetInvocationList();
            var handlerTasks = invocationList.Cast<KeyExpiredEventHandlerAsync>()
                .Select(s => s(key, applicationState)).ToArray();
            await Task.WhenAll(handlerTasks);
        }
    }

    internal Task UnsubscribeFromKeyExpirationEvents()
    {
        if (!_channelName.IsNullOrEmpty && _subscriber != null)
        {
            _logger.LogInformation("Unsubscribed app {ApplicationName}", _applicationName);
            return _subscriber.UnsubscribeAsync(_channelName);
        }
        return Task.CompletedTask;
    }

    // this is a way to make configuration simple
    // but customer should configure his cluster selfish
    private void SingleNodeConfigSet(IConnectionMultiplexer connectionMultiplexer)
    {
        const string EVENT_NAME = "notify-keyspace-events";
        try
        {
            // Pak de eerste endpoint uit de configuratie om als server te gebruiken
            var serverEndPoint = connectionMultiplexer.GetEndPoints()[0];
            var server = connectionMultiplexer.GetServer(serverEndPoint);

            // Haal de huidige configuratie op
            var currentConfig = server.ConfigGet(EVENT_NAME).FirstOrDefault().Value;

            // Controleer of 'Ex' al is ingesteld
            char[] requiredConfig = { 'x', 'E' };
            bool anyChange = false;
            foreach (var configSet in requiredConfig)
            {
                if (currentConfig.Contains(configSet))
                {
                    continue;
                }
                anyChange = true;
                currentConfig += configSet;
            }
            if (anyChange)
            {
                server.ConfigSet(EVENT_NAME, currentConfig);
                _logger.LogInformation("{EventName} is updated.", EVENT_NAME);
            }
            else
            {
                _logger.LogInformation("{EventName} already had '{requiredConfig}'", EVENT_NAME, new string(requiredConfig));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot set {EventName}", EVENT_NAME);
        }
    }
    internal async Task SubscribeToKeyExpirationEvents()
    {
        await using var initScope = _serviceProvider.CreateAsyncScope();
        var globalState = initScope.ServiceProvider.GetRequiredService<GlobalState>();
        _applicationName = globalState.ApplicationName;

        var multiplexer = await _connectionMultiplexer.GetConnectionMultiplexerAsync(true);
        if (globalState.SubscribeExpireEvents)
        {
            SingleNodeConfigSet(multiplexer);
        }

        var no = multiplexer.GetDatabase().Database;
        _subscriber = multiplexer.GetSubscriber();
        _channelName = new($"__keyevent@{no}__:expired", RedisChannel.PatternMode.Literal);

        await _subscriber.SubscribeAsync(_channelName, async (channel, value) =>
        {
            try
            {
                if (!value.IsNullOrEmpty)
                {
                    var (success, fullKey) = DecryptValue(value, globalState.KeyEncryptionSecret)!;
                    if (success && fullKey!.StartsWith(globalState.ApplicationName!))
                    {
                        var keyName = fullKey[(globalState.ApplicationName!.Length + 1)..];
                        // Verwijder de key uit de set
                        await multiplexer.GetDatabase().SetRemoveAsync(EncryptKey(globalState.ApplicationName, globalState.KeyEncryptionSecret), keyName, CommandFlags.FireAndForget);
                        _logger.LogInformation("Expire key and removal of {KeyName} from {setName}", keyName, globalState.ApplicationName);
                        await using var scope = _serviceProvider.CreateAsyncScope();
                        var ApplicationState = scope.ServiceProvider.GetRequiredService<IApplicationState>();
                        await OnKeyExpiredAsync(keyName, ApplicationState);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "during event {event}", nameof(OnKeyExpiredAsync));
            }
        });
    }
    private static RedisKey EncryptKey(ReadOnlySpan<char> value, string keyEncryptionSecret) => KeyCrypto.Encrypt(value, keyEncryptionSecret, StateBroker.IV).ToArray();

    private static (bool success, string? value) DecryptValue(RedisValue key, string keyEncryptionSecret) => KeyCrypto.Decrypt((byte[])key!, keyEncryptionSecret, StateBroker.IV);
}