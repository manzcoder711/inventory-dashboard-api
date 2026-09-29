using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace InventoryApi.Middleware;

public static class RateLimitPolicies
{
    public const string Auth = "auth";

    // Generous for a person (the demo button is one attempt), tight for a password-guessing script.
    public const int AuthPermitLimit = 10;
    public static readonly TimeSpan AuthWindow = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Partitioned per client IP. On Azure this relies on UseForwardedHeaders running first;
            // otherwise every visitor shares the front end's IP, and this becomes one global limit.
            options.AddPolicy(Auth, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = AuthPermitLimit,
                        Window = AuthWindow,
                        QueueLimit = 0,
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    httpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var logger = httpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("InventoryApi.RateLimiting");
                logger.LogWarning(
                    "Rate limit hit on {Path} for client {ClientIp}",
                    httpContext.Request.Path,
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                await httpContext.Response.WriteAsJsonAsync(
                    new { message = "Too many attempts. Please wait a minute and try again." },
                    cancellationToken);
            };
        });
}
