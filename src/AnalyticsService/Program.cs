using ClickHouse.Client.ADO;
using Microsoft.AspNetCore.Authorization;
using Shared.Auth;
using Shared.Health;
using Shared.Hosting;

const int ManagementPort = 8081;

var builder = WebApplication.CreateBuilder(args);
builder.AddGracefulShutdown();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddServiceAuth(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole("admin"));
});

var connStr = builder.Configuration.GetConnectionString("ClickHouse")
    ?? throw new InvalidOperationException("ClickHouse connection string not configured");

builder.Services.AddTransient<ClickHouseConnection>(_ => new ClickHouseConnection(connStr));

builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

MapAnalyticsEndpoints(app.MapGroup("/analytics").RequireAuthorization("AdminOnly"));
MapAnalyticsEndpoints(app.MapGroup("/api/analytics").RequireAuthorization("AdminOnly"));

app.MapServiceHealthEndpoints(ManagementPort);

app.Run();

static void MapAnalyticsEndpoints(RouteGroupBuilder group)
{
    group.MapGet("/top-hotels", async (ClickHouseConnection conn) =>
    {
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT HotelId, toInt64(count()) AS RoomCount
            FROM team_06.rooms FINAL
            WHERE is_deleted = 'false'
            GROUP BY HotelId
            ORDER BY RoomCount DESC
            LIMIT 10
            """;
        using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<object>();
        while (await reader.ReadAsync())
            result.Add(new
            {
                HotelId = reader.GetInt32(0),
                RoomCount = reader.GetInt64(1)
            });
        return Results.Ok(result);
    });

    group.MapGet("/rooms-summary", async (ClickHouseConnection conn) =>
    {
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Type, toInt64(countIf(IsAvailable = 'True')) AS Available, toInt64(count()) AS Total
            FROM team_06.rooms FINAL
            WHERE is_deleted = 'false'
            GROUP BY Type
            ORDER BY Type
            """;
        using var reader = await cmd.ExecuteReaderAsync();
        var result = new List<object>();
        while (await reader.ReadAsync())
            result.Add(new
            {
                Type = reader.GetInt32(0),
                Available = reader.GetInt64(1),
                Total = reader.GetInt64(2)
            });
        return Results.Ok(result);
    });

    group.MapGet("/guests-summary", async (ClickHouseConnection conn) =>
    {
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                toInt64(count()) AS Total,
                toInt64(countIf(CreatedAt >= now() - INTERVAL 30 DAY)) AS NewLast30Days
            FROM team_06.guests FINAL
            WHERE is_deleted = 'false'
            """;
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return Results.Ok(new { Total = reader.GetInt64(0), NewLast30Days = reader.GetInt64(1) });
        return Results.Ok(new { Total = 0, NewLast30Days = 0 });
    });
}
