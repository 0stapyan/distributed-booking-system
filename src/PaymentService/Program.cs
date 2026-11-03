using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PaymentService.Data;
using PaymentService.Models;
using PaymentService.Saga;
using PaymentService.Workers;
using Shared.Health;
using Shared.Messaging;
using Shared.Saga;
using Shared.Hosting;

const int ManagementPort = 8081;

var builder = WebApplication.CreateBuilder(args);
builder.AddGracefulShutdown();

var otelEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317";
const string serviceName = "payment-service";
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
        .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = true)
        .AddSource("Npgsql")
        .AddSource(serviceName)
        .AddSource("rabbitmq")
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

builder.AddRabbitMq();
builder.Services.AddHostedService<OutboxWorker>();
builder.AddScopedMessageHandler<ChargePaymentCommand, ChargePaymentHandler>(SagaTopology.ChargePaymentQueue);

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddCheck<DbContextHealthCheck<PaymentDbContext>>("postgres", tags: [ServiceHealthExtensions.ReadyTag]);

var app = builder.Build();

if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.MigrateAsync();
    return;
}

app.MapGet("/api/internal/payments/stats", async (PaymentDbContext db) =>
{
    var total = await db.Payments.CountAsync();
    var charged = await db.Payments.CountAsync(p => p.Status == PaymentStatus.Charged);
    var failed = await db.Payments.CountAsync(p => p.Status == PaymentStatus.Failed);
    return Results.Ok(new { Total = total, Charged = charged, Failed = failed });
});

app.MapServiceHealthEndpoints(ManagementPort);

app.Run();
