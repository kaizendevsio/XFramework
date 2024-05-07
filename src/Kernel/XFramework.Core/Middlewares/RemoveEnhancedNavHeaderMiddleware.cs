using Microsoft.AspNetCore.Http;

namespace XFramework.Core.Middlewares;

public class RemoveEnhancedNavHeaderMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            // Check and remove the Blazor-Enhanced-Nav header
            if (context.Response.Headers.ContainsKey("blazor-enhanced-nav"))
            {
                context.Response.Headers.Remove("blazor-enhanced-nav");
            }
            return Task.CompletedTask;
        });

        await next(context);
    }
}
