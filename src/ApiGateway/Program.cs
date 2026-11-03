using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using RedisRateLimiting;
using Shared.Auth;
using Shared.Health;
using StackExchange.Redis;
using Yarp.ReverseProxy.Transforms;
using Shared.Hosting;

const int ManagementPort = 8081;
const string RateLimitPolicy = "per-client-sliding-window";

var builder = WebApplication.CreateBuilder(args);
builder.AddGracefulShutdown();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.AddBffAuth(options =>
{
    options.AddPolicy("CustomerOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("customer"));
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx => ctx.RequestTransforms.Add(
        new BearerTokenTransform(ctx.Services.GetRequiredService<ILogger<BearerTokenTransform>>())));

builder.Services.AddHealthChecks();

var redisHost = builder.Configuration["Redis:Host"] ?? "redis-master.team-06.svc.cluster.local:6379";
var rateLimitRedis = new Lazy<ConnectionMultiplexer>(() =>
{
    var options = ConfigurationOptions.Parse(redisHost);
    options.Password = builder.Configuration["Redis:Password"];
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicy, httpContext =>
    {
        var partitionKey = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RedisRateLimitPartition.GetSlidingWindowRateLimiter(partitionKey, _ => new RedisSlidingWindowRateLimiterOptions
        {
            ConnectionMultiplexerFactory = () => rateLimitRedis.Value,
            PermitLimit = builder.Configuration.GetValue("RateLimit:PermitLimit", 100),
            Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimit:WindowSeconds", 60)),
        });
    });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/auth/login", () => Results.Redirect("/"))
   .AllowAnonymous();

app.MapGet("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
}).AllowAnonymous();

app.MapServiceHealthEndpoints(ManagementPort);

app.MapReverseProxy().RequireAuthorization("CustomerOnly").RequireRateLimiting(RateLimitPolicy);

app.Run();