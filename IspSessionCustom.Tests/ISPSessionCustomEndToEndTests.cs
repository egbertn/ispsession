using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace NCV.ISPSession.Tests;

public class ISPSessionCustomEndToEndTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private TestingContext GetTestingContext()
    {
        CookieContainerHandler cookieContainerHandler = new();
        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<ISPSessionRuntimeOptions>(opt =>
                {


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
        var guid = Guid.NewGuid().ToString();
        var response = await testingContext.Client.GetFromJsonAsync<CounterResponse>($"/counterWithApp?sessionId={guid}");
        Assert.NotNull(response);
        Assert.Equal(1, response.SessionCounter);
        response = await testingContext.Client.GetFromJsonAsync<CounterResponse>($"/counterWithApp?sessionId={guid}");
        Assert.NotNull(response);
        Assert.Equal(2, response.SessionCounter);
        // will always increment unless you flush redis
        Assert.True(response.AppCounter>=2);
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
        var guid = Guid.NewGuid().ToString();
        var response = await testingContext.Client.GetFromJsonAsync<CounterResponse>($"/counter?sessionId={guid}");
        Assert.NotNull(response);
        Assert.True(response.IsNewSession);
        Assert.True(response.IsExpiredSession); //expired is here always same as isNew
        Assert.Equal(1, response.SessionCounter);
        response = await testingContext.Client.GetFromJsonAsync<CounterResponse>($"/counter?sessionId={guid}");
        Assert.NotNull(response);
         Assert.False(response.IsNewSession);
        Assert.False(response.IsExpiredSession); //expired is here always same as isNew
        Assert.Equal(2, response.SessionCounter);
    }
}