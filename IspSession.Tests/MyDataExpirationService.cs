using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NCV.ISPSession.Tests;

/// <summary>
/// Deals with Redis expiration notifications
/// </summary>
public class MyDataExpirationService(
    KeyExpiredEventHook keyExpiredEventHook,
    ILogger<MyDataExpirationService> logger
    ) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        keyExpiredEventHook.KeyExpiredAsync += (key, appState) =>
        {
            logger.LogInformation("The following application key just has expired: {key}", key);
            //let us set some new random value
            appState.Set(key, Random.Shared.Next());
            return Task.CompletedTask;
        };
        try 
        {
            await Task.Delay(-1, stoppingToken);
        }
        catch(TaskCanceledException)
        {
            //no problem
        }
    }
}