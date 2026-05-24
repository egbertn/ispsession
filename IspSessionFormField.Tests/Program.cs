using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NCV.ISPSession.Tests;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(i => i.SingleLine = true);
        builder.Host.UseEnvironment(Environments.Development);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
        var services = builder.Services;

        services.AddISPSessionService(options =>
        {
            //note these options already have defaults for easy start
            // for demo purpose we show how to use it.
            options.ApplicationName = "Demo";
            options.CompressData = true;
            options.AffinityMethod = AffinityMethods.FormField;
            options.SessionCookieName = "uniquefield";
            options.DataTimeOut = TimeSpan.FromMinutes(20);
            options.Mode = UseMode.Both;
        });
        services.AddHostedService<MyDataExpirationService>();

        var app = builder.Build();
        app.MapPost("/counter", (ISessionState sessionState) =>
        {
            var counter = sessionState.Get<int>("Counter");
            counter++;
            sessionState.Set("Counter", counter);
            return Results.Ok(new
            {
                SessionCounter = counter,
                IsExpiredSession = sessionState.IsExpired,
                IsNewSession = sessionState.IsNew,
                SessionId = sessionState.SessionId
            });
        });

        app.MapPost("/counterWithApp", (ISessionState sessionState, IApplicationState appState) =>
        {
            var counter = sessionState.Get<int>("Counter");
            counter++;
            sessionState.Set("Counter", counter);
            var appCounter = appState.Get<int>("Counter");
            appCounter++;
            appState.Set("Counter", appCounter);
            return Results.Ok( new
            {
                SessionCounter = counter,
                IsNewSession = sessionState.IsNew,
                IsExpiredSession = sessionState.IsExpired,
                SessionId = sessionState.SessionId,
                AppCounter = appCounter
            });
        });


        app.UseISPSession();

        app.Run();

    }
}