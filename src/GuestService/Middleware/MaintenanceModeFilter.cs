using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace GuestService.Middleware;

public class MaintenanceModeFilter(
    IOptionsMonitor<MaintenanceModeOptions> options,
    ILogger<MaintenanceModeFilter> logger) : IAsyncResourceFilter
{
    private static readonly HashSet<string> ReadMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD" };

    private static readonly HashSet<string> WriteMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var opts   = options.CurrentValue;
        var method = context.HttpContext.Request.Method;

        var blocked = (opts.Write && WriteMethods.Contains(method))
                   || (opts.Read  && ReadMethods.Contains(method));

        if (blocked)
        {
            logger.LogWarning(
                "Request blocked by maintenance mode. Method={Method} Path={Path}",
                method, context.HttpContext.Request.Path);

            context.Result = new ObjectResult(new
            {
                message = "Service is under maintenance. Please try again later.",
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };

            return;
        }

        await next();
    }
}