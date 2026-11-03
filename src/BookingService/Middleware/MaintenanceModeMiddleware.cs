namespace BookingService.Middleware;

public class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;

    public MaintenanceModeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var enabled = Environment.GetEnvironmentVariable("MAINTENANCE_MODE");
        if (string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            var method = context.Request.Method;
            if (method == HttpMethods.Post || method == HttpMethods.Put ||
                method == HttpMethods.Patch || method == HttpMethods.Delete)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"message\":\"Service is under maintenance. Please try again later.\"}");
                return;
            }
        }

        await _next(context);
    }
}
