

namespace NCV.ISPSession.Demo;

public class MyDataExpirationService(
    KeyExpiredEventHook _keyExpiredEventHook,
    ILogger<MyDataExpirationService> _logger,
    IServiceScopeFactory _serviceScopeFactory
    ) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = _serviceScopeFactory.CreateAsyncScope())
        {
            var applicationState = scope.ServiceProvider.GetRequiredService<IApplicationState>();
            var persistComplexData = new ServiceDto
            {
                Description = "test",
                Id = 1,
                ImageUri = "image.jpg",
                Name = "test",
                NumberOfBeds = 2,
                Properties = new Dictionary<string, string> { { "key", "test" } },
                ServiceTimes = [
                    new () { Id = 1, Duration=TimeSpan.FromMinutes(70), Price=60, TreatmentTime = TimeSpan.FromMinutes(60)}
            ]
            };
            applicationState.Set("complex_data", persistComplexData);
            ICollection<FeatureDto> features = [new() { Id = 1, Name = "test", Price = 1203.4M, RequiredTime = new TimeSpan(1, 0, 0) }];
            applicationState.Set("features", features);
        }


        _keyExpiredEventHook.KeyExpiredAsync += (key, appState) =>
        {
            _logger.LogInformation("The following application key just has expired: {key}", key);
            // set some random integer to prove it worked
            appState.Set(key, Random.Shared.Next());
            return Task.CompletedTask;
        };
        await Task.Delay(-1, stoppingToken);
    }
}