using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Transforms;

namespace Shared.Auth;

public class BearerTokenTransform(ILogger<BearerTokenTransform> logger) : RequestTransform
{
    public override async ValueTask ApplyAsync(RequestTransformContext context)
    {
        if (context.HttpContext.Request.Headers.ContainsKey("Authorization"))
            return;

        var token = await context.HttpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(token))
        {
            context.ProxyRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            var user = context.HttpContext.User.Identity?.Name ?? "unknown";
            logger.LogInformation("[bff-transform] cookie→Bearer injected for {User} on {Path}",
                user, context.HttpContext.Request.Path);
        }
    }
}