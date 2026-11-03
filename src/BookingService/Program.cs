using BookingService.Application.Clients;
using BookingService.Application.Repositories;
using BookingService.Application.Resilience;
using BookingService.Application.Services;
using BookingService.Application.Telemetry;
using BookingService.Data;
using BookingService.Http;
using BookingService.Middleware;
using BookingService.Repositories;
using BookingService.Saga;
using BookingService.Workers;
using BookingService.Cdc;
using BookingService.ReadModels;
using BookingService.Application.ReadModels;
using SagaMetrics = BookingService.Saga.SagaMetrics;
using Shared.Auth;
using Shared.Cdc;
using Shared.Health;
using Shared.Messaging;
using Shared.Saga;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.Hosting;

const int ManagementPort = 8081;

var builder = WebApplication.CreateBuilder(args);
builder.AddGracefulShutdown();

var otelEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317";
const string serviceName = "booking-service";
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
        .AddAspNetCoreInstrumentation(o =>
        {
            o.RecordException = true;
            o.Filter = ctx => ctx.Request.Path.Value != "/health";
        })
        .AddHttpClientInstrumentation(o => o.RecordException = true)
        .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = true)
        .AddSource("Npgsql")
        .AddSource(serviceName)
        .AddSource("rabbitmq")
        .AddSource("Polly")
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(BookingMetrics.MeterName)
        .AddMeter(SagaMetrics.MeterName)
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

builder.AddServiceAuth();

builder.Services.AddControllers(o => o.Filters.Add<TracingActionFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<BookingMetrics>();
builder.Services.AddSingleton<SagaMetrics>();

builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IBookingsService, BookingsService>();
builder.Services.AddScoped<IMasterDataReader, MasterDataReader>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(sp =>
    GuestScoreResiliencePipelineFactory.Create(sp.GetRequiredService<ILoggerFactory>()));
builder.Services.AddHttpClient<IGuestScoreClient, GuestScoreClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["Services:GuestScore"] ?? "http://guest-score-service"));

builder.AddRabbitMq();
builder.Services.AddHostedService<OutboxWorker>();

builder.AddKafkaCdc();
builder.AddCdcProjector<RoomReplicaProjector>("team-06.hotel.public.Rooms", "team-06.booking.rooms-replica");
builder.AddCdcProjector<GuestReplicaProjector>("team-06.guests.public.Guests", "team-06.booking.guests-replica");

builder.AddScopedMessageHandler<RoomReservedEvent, RoomReservedHandler>(SagaTopology.RoomReservedQueue);
builder.AddScopedMessageHandler<RoomReservationFailedEvent, RoomReservationFailedHandler>(SagaTopology.RoomReservationFailedQueue);
builder.AddScopedMessageHandler<PaymentChargedEvent, PaymentChargedHandler>(SagaTopology.PaymentChargedQueue);
builder.AddScopedMessageHandler<PaymentFailedEvent, PaymentFailedHandler>(SagaTopology.PaymentFailedQueue);
builder.AddScopedMessageHandler<RoomReleasedEvent, RoomReleasedHandler>(SagaTopology.RoomReleasedQueue);

builder.Services.AddDbContext<BookingServiceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddCheck<DbContextHealthCheck<BookingServiceDbContext>>("postgres", tags: [ServiceHealthExtensions.ReadyTag]);

var app = builder.Build();

if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BookingServiceDbContext>();
    await db.Database.MigrateAsync();
    return;
}

app.UseMiddleware<MaintenanceModeMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceHealthEndpoints(ManagementPort);
app.Run();
