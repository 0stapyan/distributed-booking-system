using System.Diagnostics;
using GuestService.Data;
using Microsoft.EntityFrameworkCore;
using PactNet.Verifier;
using Shared.Models;
using Testcontainers.PostgreSql;
using Xunit;

namespace GuestService.Tests;

public sealed class GuestServiceProviderVerificationTests : IAsyncLifetime
{
    private const int AppPort = 18080;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("guest_provider_verification")
        .Build();

    private Process _process = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();

            db.Guests.Add(new Guest
            {
                Id = 1,
                FirstName = "Contract",
                LastName = "Guest",
                Email = "contract@example.com",
                Phone = "+380000000000",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            await db.SaveChangesAsync();
        }

        var dllPath = Path.Combine(AppContext.BaseDirectory, "GuestService.dll");
        var startInfo = new ProcessStartInfo("dotnet", dllPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            EnvironmentVariables =
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{AppPort}",
                ["ConnectionStrings__DefaultConnection"] = _postgres.GetConnectionString(),
                ["Otel__Endpoint"] = "http://127.0.0.1:1",
            },
        };
        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start GuestService");

        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
                throw new InvalidOperationException(
                    $"GuestService exited early (code {_process.ExitCode}):\n{await _process.StandardError.ReadToEndAsync()}");
            try
            {
                var probe = await http.GetAsync($"http://127.0.0.1:{AppPort}/api/internal/guests/1");
                if (probe.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(500);
        }
        throw new TimeoutException("GuestService never became ready");
    }

    public async Task DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        _process.Dispose();
        await _postgres.DisposeAsync();
    }

    private GuestDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<GuestDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public void GuestService_SatisfiesGuestScoreServiceContract()
    {
        var pactPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "pacts", "GuestScoreService-GuestService.json");

        new PactVerifier(nameof(GuestServiceProviderVerificationTests))
            .WithHttpEndpoint(new Uri($"http://127.0.0.1:{AppPort}"))
            .WithFileSource(new FileInfo(pactPath))
            .Verify();
    }
}