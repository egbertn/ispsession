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

        Dictionary<string, string> fields = new() { { "uniquefield", Guid.NewGuid().ToString() } };

        using var responseContent = await testingContext.Client.PostAsync("/counterWithApp", new FormUrlEncodedContent(fields));

        var response = await responseContent.Content.ReadFromJsonAsync<CounterResponse>();
        Assert.NotNull(response);
        Assert.Equal(1, response.SessionCounter);
        var responseContent2 = await testingContext.Client.PostAsync("/counterWithApp", new FormUrlEncodedContent(fields));

        response = await responseContent2.Content.ReadFromJsonAsync<CounterResponse>();

        Assert.NotNull(response);
        Assert.Equal(2, response.SessionCounter);
        // will always increment unless you flush redis
        Assert.True(response.AppCounter>=2);
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