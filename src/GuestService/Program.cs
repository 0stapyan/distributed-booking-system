using GuestService.Application.Metrics;
using GuestService.Application.Repositories;
using GuestService.Application.Services;
using GuestService.Data;
using GuestService.Middleware;
using GuestService.Repositories;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.Health;
using Shared.Hosting;

const int ManagementPort = 8081;

var builder = WebApplication.CreateBuilder(args);
builder.AddGracefulShutdown();

var otelEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317";
const string serviceName = "guest-service";
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
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)))
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(GuestMetrics.MeterName)
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IGuestMetrics, GuestMetrics>();
builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

builder.Services.Configure<MaintenanceModeOptions>(
    builder.Configuration.GetSection(MaintenanceModeOptions.SectionName));
builder.Services.AddTransient<MaintenanceModeFilter>();

builder.Services.AddControllers(o =>
{
    o.Filters.Add<MaintenanceModeFilter>();
    o.Filters.Add<TracingActionFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddScoped<IGuestsService, GuestsService>();

builder.Services.AddDbContext<GuestDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddCheck<DbContextHealthCheck<GuestDbContext>>("postgres", tags: [ServiceHealthExtensions.ReadyTag]);

var app = builder.Build();

if (args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GuestDbContext>();
    await db.Database.MigrateAsync();
    Console.WriteLine("Migrations applied successfully.");
    return;
}

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/api/internal/guests/{id:int}", async (int id, GuestDbContext db) =>
{
    var guest = await db.Guests.FindAsync(id);
    return Results.Ok(new
    {
        GuestId = id,
        Exists = guest is not null,
        CreatedAt = guest?.CreatedAt ?? DateTime.UnixEpoch
    });
});

app.MapControllers();
app.MapServiceHealthEndpoints(ManagementPort);
app.Run();