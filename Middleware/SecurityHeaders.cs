namespace InventoryApi.Middleware;

public static class SecurityHeaders
{
    public static IApplicationBuilder UseApiSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            // OnStarting runs just before the response is sent. Setting the headers directly here
            // would lose them on error responses, because the exception handler clears the
            // response's headers before writing its 500.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";

                // The API only returns JSON, so nothing in a response ever needs to load, run,
                // or be framed - the strictest possible policy costs nothing.
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                return Task.CompletedTask;
            });

            await next();
        });
}
