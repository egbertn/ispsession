using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCV.ISPSession.Internal;

namespace NCV.ISPSession;

/// <summary>
/// specify which modules to activate
/// </summary>
public enum UseMode
{
    /// <summary>
    /// activates ISPSession only
    /// </summary>
    ISPSession,
    /// <summary>
    /// activates both ISPSession and ISPApplication
    /// </summary>
    Both,
    /// <summary>
    /// activates ISPApplication (cache) only
    /// </summary>
    ISPApplication
}

/// <summary>
/// Extensions for using and configuring ISP Session
/// </summary>
public static class ExtensionMethods
{
    /// <summary>
    /// Gets an instance of ISessionState with using your custom SessionId
    /// </summary>
    /// <param name="context"></param>
    /// <param name="sessionId">You are responsible for giving a unique session id</param>
    public static async Task< ISessionState> GetCustomSessionId(this HttpContext context, string sessionId)
    {
        var sessionState =(SessionState) context.RequestServices.GetRequiredService<ISessionState>() ;
        var settings = context.RequestServices.GetRequiredService<GlobalState>();
        if (settings.Affinity != AffinityMethods.CustomInit)
        {
            throw new InvalidOperationException("Affinity must be set to CustomInit before you can issue GetCustomSessionId");
        }
        await sessionState.InitLateAsync(sessionId);
        return sessionState;
    }
    /// <summary>
    /// Registers ISP Session Middleware
    /// depending on how you configured the Mode during AddISPSessionService options
    /// By default, Both ISP Session State and Application State will be set
    /// If you do not need either Session State or Application State
    /// make sure to enable only the one you need for resources optimization
    /// </summary>
    public static IApplicationBuilder UseISPSession(this IApplicationBuilder builder)
    {
        GlobalState globalState = builder.ApplicationServices.GetRequiredService<GlobalState>();
        Type type = globalState.Mode switch
        {
            UseMode.Both => typeof(SessionApplicationMiddleware),
            UseMode.ISPApplication => typeof(ApplicationMiddleware),
            UseMode.ISPSession => typeof(SessionMiddleware),
            _ => throw new NotImplementedException()
        };
        return builder.UseMiddleware(type);
    }

    /// <summary>
    /// Registers ISP Session Services in the DI Engine of ASP.NET Core
    /// </summary>
    public static void AddISPSessionService(this IServiceCollection services,
        Action<ISPSessionRuntimeOptions>? options = null)
    {
        ISPSessionRuntimeOptions defaultOptions = new();
        options?.Invoke(defaultOptions);

        services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var section = configuration.GetSection("IspSession") ?? throw new InvalidOperationException("missing IspSession section in appsettings.json, e.g. \"ConnectionStrings:IspSession\": \"localhost:6379,defaultDatabase=1,ssl=False\"");
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();

            defaultOptions.CookieSecure |=hostEnvironment.IsProduction() || hostEnvironment.IsStaging();

            ISPSessionOptions iSPSessionOptions = new()
            {
                KeyEncryptionSecret = section.GetValue<string>("KeyEncryptionSecret") ?? throw new InvalidOperationException("missing KeyEncryptionSecret"),
                MonitorSessionKey = section.GetValue<string?>("MonitorSessionKey"),
            };

            return new GlobalState(iSPSessionOptions, defaultOptions);
        });

        services.AddScoped(sp => new StateBroker(
            sp.GetRequiredService<IISPSessionConnectionMultiplexer>(),
            sp.GetRequiredService<GlobalState>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<StateBroker>())
        );

        if (defaultOptions.Mode == UseMode.ISPSession || defaultOptions.Mode == UseMode.Both)
        {
            services.AddScoped<ISessionState>(sp => new SessionState(sp.GetRequiredService<GlobalState>(), sp.GetRequiredService<StateBroker>()));
        }

        if (defaultOptions.Mode == UseMode.ISPApplication || defaultOptions.Mode == UseMode.Both)
        {
            services.AddScoped<IApplicationState>(sp => new ApplicationState(sp.GetRequiredService<StateBroker>()));
            services.AddScoped<IApplicationStateUnbound>(sp =>
            {
                return new ApplicationState(sp.GetRequiredService<StateBroker>());
            });
        }

        services.AddSingleton(sp =>
            new KeyExpiredEventHook(
                sp.GetRequiredService<IISPSessionConnectionMultiplexer>(),
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<KeyExpiredEventHook>(),
                sp,
                sp.GetRequiredService<GlobalState>()
                )
        );
        services.AddHostedService(sp => new ExpirationEventListener(sp.GetRequiredService<KeyExpiredEventHook>()));
        services.AddSingleton<IISPSessionConnectionMultiplexer, ISPSessionConnectionMultiplexer>();
    }
}