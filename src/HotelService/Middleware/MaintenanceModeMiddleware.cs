namespace HotelService.Middleware;

public class MaintenanceModeMiddleware
{
    private static readonly string[] WriteMethods = ["POST", "PUT", "PATCH", "DELETE"];
    private readonly RequestDelegate _next;

    public MaintenanceModeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var writeMM = Environment.GetEnvironmentVariable("MaintenanceMode__Write");
        if (string.Equals(writeMM, "true", StringComparison.OrdinalIgnoreCase)
            && WriteMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 503;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"message\":\"Service is under maintenance. Please try again later.\"}");
            return;
        }

        await _next(context);
    }
}
