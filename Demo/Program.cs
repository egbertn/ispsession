using Microsoft.AspNetCore.Mvc;
using NCV.ISPSession;
using NCV.ISPSession.Demo;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(i => i.SingleLine = true);

services.AddISPSessionService(options =>
{
    //note these options already have defaults for easy start
    // for demo purpose we show how to use it.
    options.ApplicationName = "Demo";
    options.CompressData = true;
    //options.AffinityMethod = AffinityMethods.FormField;
    //options.SessionCookieName = "uniquefield";
    options.Mode = UseMode.Both;
});

services.AddHostedService<MyDataExpirationService>();
services.AddHostedService<ScriptUpdateWatcher>();
var app = builder.Build();

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{

    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateTime.Today.AddDays(index),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Map("/counter", (ISessionState sessionState) =>
{
    var counter = sessionState.Get<int>("Counter");
    counter++;
    sessionState.Set("Counter", counter);
    return new
    {
        SessionCounter = counter,
        IsExpiredSession = sessionState.IsExpired,
        IsNewSession    = sessionState.IsNew,
        SessionId = sessionState.SessionId
    };
});

 app.MapGet("/complexdata", (IApplicationState appState) =>
{
    var features = appState.Get<ICollection<FeatureDto>>("features");
    var persistComplexData = appState.Get<ServiceDto>("complex_data");
    return persistComplexData;

});

app.MapGet("/apponly", (IApplicationState appState) =>
{
     var appCounter = appState.Get<int>("Counter");
     var hello = appState.Get<string?>("string");
    appCounter++;
    appState.Set("Counter", appCounter);
    appState.Set("string", $"hello world {Random.Shared.Next()}");
    return new
    {
        AppCounter = appCounter,
        Hello = hello
    };
});
app.UseISPSession();

 app.MapGet("/abandon", (HttpContext httpContext, ISessionState sessionState) =>
{
    sessionState.Abandon(httpContext);
});

app.MapGet("/appkeyexpire", (IApplicationState appState) =>
{
    appState.ExpireKeyAt("Counter", TimeSpan.FromSeconds(1));
});

app.MapGet("/counterWithApp", (ISessionState sessionState, IApplicationState appState) =>
{
    var counter = sessionState.Get<int>("Counter");
    int counter2=0;
    if (counter == 0)
    {
            //trigger compression
            sessionState.Set("huge_string", new string('*', 1000));
    }
    if (counter < 10)
    {
        counter++;
        if (sessionState.Get<string>("huge_string") != new string('*', 1000))
        {
            throw new Exception("Data integrity failed");
        }

        sessionState.Set("Counter", counter);
    }
    else
    {
        counter2 = sessionState.Get<int>("Counter2");
        counter2++;
        sessionState.Set("Counter2", counter2);
    }
    var appCounter = appState.Get<int>("Counter");
    appCounter++;
    appState.Set("Counter", appCounter);

    return new
    {
        SessionCounter = counter,
        SessionCounter2 = counter2,
        IsNewSession = sessionState.IsNew,
        IsExpiredSession = sessionState.IsExpired,
        SessionId = sessionState.SessionId,
        AppCounter = appCounter,


    };
});

app.Run();

record WeatherForecast(DateTime Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
