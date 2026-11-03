using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shared.Health;

public static class ServiceHealthExtensions
{
    public const string ReadyTag = "ready";

    public static void MapServiceHealthEndpoints(this IEndpointRouteBuilder endpoints, int managementPort)
    {
        var management = endpoints.MapGroup("").RequireHost($"*:{managementPort}");

        management.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        }).AllowAnonymous();

        management.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag)
        }).AllowAnonymous();
    }
}
