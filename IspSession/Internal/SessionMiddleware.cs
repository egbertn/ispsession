using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;

namespace NCV.ISPSession.Internal;

internal sealed class SessionMiddleware(RequestDelegate next)
{
    private readonly FileExtensionContentTypeProvider _fileExtensionContentTypeProvider = new();

    private bool IsStaticFileRequest(PathString path) => _fileExtensionContentTypeProvider.TryGetContentType(path, out _);

    public async Task InvokeAsync(HttpContext context, ISessionState iSessionState)
    {
        if (IsStaticFileRequest(context.Request.Path))
        {
            await next(context);
            return;
        }
        var sessionState = (SessionState)iSessionState ;
        await sessionState.InitAsync(context);        
        await next(context);
    }           
}