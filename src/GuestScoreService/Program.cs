using GuestScoreService.Resilience;
using GuestScoreService.Services;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.Auth;
using Shared.Health;
using Shared.Hosting;

const int ManagementPort = 8081;

var builder = WebApplication.CreateBuilder(args);
builder.AddGracefulShutdown();

var otelEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317";
const string serviceName = "guest-score-service";
const string teamName = "team-06";

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName)
    .AddAttributes(new Dictionary<string, object>
    {
        ["team"] = teamName,
        ["deployment.environment"] = builder.Environment.EnvironmentName.ToLowerInvariant(),
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(resourceBuilder);
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
    logging.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
});

builder.Logging.AddFilter("OpenTelemetry", LogLevel.Warning);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation(o => o.Filter = ctx => ctx.Request.Path.Value != "/health")
        .AddHttpClientInstrumentation(o => o.RecordException = true)
        .AddSource(serviceName)
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

builder.AddServiceAuth();

builder.Services.AddHttpClient("guest-service", client =>
        client.BaseAddress = new Uri(builder.Configuration["Services:GuestService"] ?? "http://guest-service"))
    .AddResilienceHandler("guest-service-pipeline", DependencyResilience.Configure);
builder.Services.AddHttpClient("payment-service", client =>
        client.BaseAddress = new Uri(builder.Configuration["Services:PaymentService"] ?? "http://payment-service"))
    .AddResilienceHandler("payment-service-pipeline", DependencyResilience.Configure);

builder.Services.AddScoped<IGuestScoreEvaluator, GuestScoreEvaluator>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceHealthEndpoints(ManagementPort);

app.Logger.LogInformation("GuestScoreService ready to accept traffic");

app.Run();