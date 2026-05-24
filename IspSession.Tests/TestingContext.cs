using System.Net;

namespace NCV.ISPSession.Tests;

public sealed class TestingContext(
HttpClient client,
CookieContainer cookieContainer,
IServiceProvider serviceProvider) : IDisposable
{
    public HttpClient Client { get => client; }

    public CookieContainer CookieContainer { get => cookieContainer; }

    public IServiceProvider Services { get => serviceProvider; }

    public void Dispose()
    {
        Client?.Dispose();
    }
}