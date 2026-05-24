using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCV.ISPSession.Internal;

namespace NCV.ISPSession.Tests;

public class ISPSessionEndToEndTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private TestingContext GetTestingContext( AffinityMethods affinityMethods = AffinityMethods.Cookie, string? key=null)
    {
        CookieContainerHandler cookieContainerHandler = new();
        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<ISPSessionRuntimeOptions>(opt =>
                {
                    opt.AffinityMethod = affinityMethods;
                    if (key != null)
                    {
                        opt.SessionCookieName = key;
                    }
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.Configure<ISPSessionRuntimeOptions>(opt =>
                {
                    opt.AffinityMethod = affinityMethods;
                    if (key != null)
                    {
                        opt.SessionCookieName = key;
                    }
                });
            });

        }).CreateDefaultClient(cookieContainerHandler);

        return new TestingContext(
            client,
            cookieContainerHandler.Container,
            factory.Services);
    }

    public class CounterResponse
    {
        public int SessionCounter { get; set; }
        public int? AppCounter { get; set; }
        public bool? IsNewSession { get; set; }
        public bool? IsExpiredSession { get; set; }
        public string? SessionId { get; set; }
    }

    [Fact]
    public async Task SaveAndLoadSessionAndAplication()
    {
        //we need instead of mocking the world a real
        // world unit test matching client server requests
        // because
        // 1. encryption
        // 2. compression
        // 3. complexity
        using var testingContext = GetTestingContext();

        var response = await testingContext.Client.GetFromJsonAsync<CounterResponse>("/counterWithApp");
        Assert.NotNull(response);
        Assert.Equal(1, response.SessionCounter);
        response = await testingContext.Client.GetFromJsonAsync<CounterResponse>("/counterWithApp");
        Assert.NotNull(response);
        Assert.Equal(2, response.SessionCounter);
        // will always increment unless you flush redis
        Assert.True(response.AppCounter>=2);
    }

    /// <summary>
    /// Proves that no session cookie is generated for static content
    /// </summary>
    [Theory]
    [InlineData("man_with_gun.jpeg")]
    [InlineData("favicon.ico")]
    public async Task ShouldSkipIgnoreContent(string file)
    {
        using var testingContext = GetTestingContext();
        var response = await testingContext.Client.GetAsync(file);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cookies = testingContext.CookieContainer.GetAllCookies();
        Assert.Empty(cookies);
    }

    [Fact]
    public async Task ApplicationKeyExpires()
    {
        //we need instead of mocking the world a real
        // world unit test matching client server requests
        // because
        // 1. encryption
        // 2. compression
        // 3. complexity
        using var testingContext = GetTestingContext();
        var response = await testingContext.Client.GetFromJsonAsync<CounterResponse>("/counterWithApp");

        await testingContext.Client.GetAsync("/appkeyexpire");
        await Task.Delay(TimeSpan.FromSeconds(3));
        response = await testingContext.Client.GetFromJsonAsync<CounterResponse>("/counterWithApp");
        Assert.NotNull(response);
        // appCounter will be set to a crazy number
        Assert.NotEqual(1,response.AppCounter);
    }

    [Fact]
    public async Task AbandonSession()
    {
        using var testingContext = GetTestingContext();

        var response = await testingContext.Client.GetFromJsonAsync<CounterResponse>("/counter");
        Assert.NotNull(response);
        Assert.True(response.IsNewSession);
        Assert.Equal(1, response.SessionCounter);
        var opt = testingContext.Services.GetRequiredService<GlobalState>();
        var serverCookie = testingContext.CookieContainer.GetAllCookies().FirstOrDefault(f => f.Name == opt.SessionCookieName);
        Assert.NotNull(serverCookie);
        await testingContext.Client.GetAsync("/abandon");

        //fake a valid cookie, for which obviously no data exists at backend
        var fakeCookieValue = Guid.NewGuid().ToString();
        testingContext.CookieContainer.Add(new Cookie("ispsession", fakeCookieValue)
        {
            Domain = serverCookie.Domain,
            Secure = false,
            Port = serverCookie.Port,
            Path = serverCookie.Path,
            HttpOnly = true
        });
        response = await testingContext.Client.GetFromJsonAsync<CounterResponse>("/counter");

        Assert.NotNull(response);
        Assert.True(response.IsExpiredSession);
        Assert.Equal(1, response.SessionCounter);
        Assert.NotEqual(fakeCookieValue, response.SessionId);
    }
    [Fact]
    public async Task SaveAndLoadSession()
    {
        //we need instead of mocking the world a real
        // world unit test matching client server requests
        // because
        // 1. encryption
        // 2. compression
        // 3. complexity
        using var testingContext = GetTestingContext();

        var response = await testingContext.Client.GetFromJsonAsync<CounterResponse>("/counter");
        Assert.NotNull(response);
        Assert.True(response.IsNewSession);
        Assert.Equal(1, response.SessionCounter);
        response = await testingContext.Client.GetFromJsonAsync<CounterResponse>("/counter");
        Assert.NotNull(response);
        Assert.Equal(2, response.SessionCounter);
    }
}