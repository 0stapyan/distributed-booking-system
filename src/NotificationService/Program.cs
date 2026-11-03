using NotificationService.Observability;
using NotificationService.Workers;
using Shared.Health;
using Shared.Messaging;
using Shared.Saga;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.Hosting;

const int ManagementPort = 8081;

var builder = WebApplication.CreateBuilder(args);
builder.AddGracefulShutdown();

var otelEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317";
const string serviceName = "notification-service";
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
        .AddSource(serviceName)
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(NotificationMetrics.MeterName)
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

builder.Services.AddSingleton<NotificationMetrics>();
builder.AddRabbitMq();
builder.AddScopedMessageHandler<BookingCreatedMessage, BookingCreatedHandler>("team-06.bookings.booking-created");
builder.AddScopedMessageHandler<BookingConfirmedEvent, BookingConfirmedHandler>(SagaTopology.BookingConfirmedQueue);
builder.AddScopedMessageHandler<BookingCancelledEvent, BookingCancelledHandler>(SagaTopology.BookingCancelledQueue);

builder.Services.AddHealthChecks()
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [ServiceHealthExtensions.ReadyTag]);

var app = builder.Build();

app.MapServiceHealthEndpoints(ManagementPort);

app.Run();
