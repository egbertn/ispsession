
using System.Security.Cryptography;

namespace NCV.ISPSession.Demo;

/// <summary>
/// Showcase how to use an HTTP Unbound service
/// to deal with cached data
/// In this example we monitor one file and calculate it's hash
/// and deal with 'if the file really was changed', do that...
/// </summary>
public sealed class ScriptUpdateWatcher(
    ILogger<ScriptUpdateWatcher> logger,
    IWebHostEnvironment env,
    IServiceScopeFactory serviceScopeFactory
) : BackgroundService
{

    public static async Task<string> ComputeSha256(string tarPathFile, CancellationToken cancel)
   {
      await using var stream = File.OpenRead(tarPathFile);
      var hash = await SHA256.HashDataAsync( stream, cancel);
      return Convert.ToHexString(hash).ToLowerInvariant();
   }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scriptsPath = env.WebRootPath!;
        var script = "test.js";
        using FileSystemWatcher _watcher = new (scriptsPath, "*.js");
        // make sure that our state instance will be disposed in the scope
        await using (var scope = serviceScopeFactory.CreateAsyncScope())
        {
            var state = scope.ServiceProvider.GetRequiredService<IApplicationStateUnbound>();
            await state.EnsureInitializedAsync();
            if (state.Get<string>($"hash_{script}")== null)
            {
                var hash = await ComputeSha256(Path.Combine(scriptsPath, script), stoppingToken);
                state.Set($"hash_{script}", hash);
            }

            _watcher.IncludeSubdirectories = false;
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            _watcher.Changed += async (_, e) => await OnScriptChanged(e.FullPath, script, stoppingToken);
            _watcher.EnableRaisingEvents = true;
            logger.LogInformation("📡 ScriptWatcher gestart op pad {Path}", scriptsPath);
        }
        try
        {
            await Task.Delay(-1, stoppingToken);
        }
        catch (OperationCanceledException)
        {}
    }
    private string? avoidDuplicateEvent;
    private async Task OnScriptChanged(string filePath, string script, CancellationToken token)
    {
        if (!filePath.EndsWith(script, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await Task.Delay(100, token); // 🕐 kleine delay om file locks te vermijden

            var hash = await ComputeSha256(filePath, token);
            if (hash == avoidDuplicateEvent)
            {
                logger.LogInformation("Avoid duplicate event");
                return;
            }
            avoidDuplicateEvent = hash;

            await using (var scope = serviceScopeFactory.CreateAsyncScope())
            {
                var state =  scope.ServiceProvider.GetRequiredService<IApplicationStateUnbound>();
                await state.EnsureInitializedAsync();
                var previousHash =state.Get<string>($"hash_{script}");
                if (hash == previousHash)
                {
                    logger.LogInformation("Duplicate Change Event avoided {file}", filePath);
                    return;
                }
                state.Set($"hash_{script}", hash);
                logger.LogInformation("📦 Script changed ({File})", filePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Fout bij verwerken script-wijziging: {Path}", filePath);
        }
    }
}