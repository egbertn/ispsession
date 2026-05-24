
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;

namespace NCV.ISPSession.Internal;

internal sealed class ApplicationMiddleware(RequestDelegate next)
{
    private readonly FileExtensionContentTypeProvider _fileExtensionContentTypeProvider = new();

    /**
      * Avoids executing ISP Session on static files (js e.g.)
      */
    private bool IsStaticFileRequest(PathString path) => _fileExtensionContentTypeProvider.TryGetContentType(path, out _);

    public async Task InvokeAsync(
        HttpContext context,
        IApplicationState iApplicationState)
    {
        if (IsStaticFileRequest(context.Request.Path))
        {
            await next(context);
            return;
        }
        var applicationState = (ApplicationState)iApplicationState;
        await applicationState.InitAsync();
        await next(context);
    }
}