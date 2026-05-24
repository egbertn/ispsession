using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace NCV.ISPSession.Tests;

public class ISPSessionFieldEndToEndTets : IClassFixture<WebApplicationFactory<ProgramFormField>>
{
    private readonly WebApplicationFactory<ProgramFormField> _factory;
    public ISPSessionFieldEndToEndTets(WebApplicationFactory<ProgramFormField> factory)
    {
        _factory = factory;
    }
    private TestingContext GetTestingContext()
    {
        CookieContainerHandler cookieContainerHandler = new();
        var client = _factory.WithWebHostBuilder(builder =>
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
            _factory.Services);
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
    public async Task FormFieldSessionSucceeds()
    {
        using var testingContext = GetTestingContext();
        Dictionary<string, string> fields = [ { "uniquefield", Guid.NewGuid().ToString() } ];

        using var responseContent = await testingContext.Client.PostAsync("/counter", new FormUrlEncodedContent(fields));
        var response = await responseContent.Content.ReadFromJsonAsync<CounterResponse>();
        Assert.NotNull(response);
        Assert.True(response.IsNewSession);
        Assert.Equal(1, response.SessionCounter);
        var serverCookie = testingContext.CookieContainer.GetAllCookies().FirstOrDefault(f => f.Name == "ispsession");
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






}