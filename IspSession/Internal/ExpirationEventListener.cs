using Microsoft.Extensions.Hosting;

namespace NCV.ISPSession.Internal;

internal sealed class ExpirationEventListener(KeyExpiredEventHook keyExpiredEventHook) : BackgroundService
{protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.Register(() => keyExpiredEventHook.UnsubscribeFromKeyExpirationEvents());

        await keyExpiredEventHook.SubscribeToKeyExpirationEvents();
    }
}