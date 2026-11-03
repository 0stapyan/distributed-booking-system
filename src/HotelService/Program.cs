using HotelService.Application.Repositories;
using HotelService.Application.Services;
using HotelService.Middleware;
using HotelService.Observability;
using HotelService.Repositories;
using HotelService.Workers;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using HotelService.Saga;
using Shared.Data;
using Shared.Health;
using Shared.Messaging;
using Shared.Saga;
using Shared.Hosting;

const int ManagementPort = 8081;

var builder = WebApplication.CreateBuilder(args);
builder.AddGracefulShutdown();

var otelEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://otel-collector:4317";
var serviceName = "hotel-service";
var teamName = "team-06";

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName)
    .AddAttributes(new Dictionary<string, object>
    {
        ["team"] = teamName,
        ["service.name"] = serviceName
    });

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation(o =>
        {
            o.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(o =>
        {
            o.SetDbStatementForText = true;
        })
        .AddSource("Npgsql")
        .AddSource(serviceName)
        .AddSource("rabbitmq")
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))

    .WithMetrics(metrics => metrics
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(HotelMetrics.MeterName)
        .AddMeter(MessagingMetrics.MeterName)
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(resourceBuilder);
    logging.IncludeScopes = true;
    logging.IncludeFormattedMessage = true;
    logging.AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint));
});
builder.Logging.AddConsole();

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();
builder.Services.AddHttpClient("default")
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<HotelMetrics>();
builder.Services.AddSingleton<MessagingMetrics>();
builder.AddRabbitMq();
builder.Services.AddHostedService<OutboxWorker>();

builder.AddScopedMessageHandler<ReserveRoomCommand, ReserveRoomHandler>(SagaTopology.ReserveRoomQueue);
builder.AddScopedMessageHandler<ReleaseRoomCommand, ReleaseRoomHandler>(SagaTopology.ReleaseRoomQueue);

builder.Services.AddScoped<IHotelRepository, HotelRepository>();
builder.Services.AddScoped<IRoomRepository, RoomRepository>();
builder.Services.AddScoped<IHotelsService, HotelsService>();
builder.Services.AddScoped<IRoomsService, RoomsService>();

builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddCheck<DbContextHealthCheck<BookingDbContext>>("postgres", tags: [ServiceHealthExtensions.ReadyTag]);

var app = builder.Build();

if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
    db.Database.Migrate();
    return;
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<MaintenanceModeMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapServiceHealthEndpoints(ManagementPort);
app.Run();