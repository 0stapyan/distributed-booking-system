using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PactNet.Verifier;
using PaymentService.Data;
using PaymentService.Models;
using RabbitMQ.Client;
using Shared.Saga;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace PaymentService.Tests;

public sealed class PaymentServiceProviderVerificationTests : IAsyncLifetime
{
    private const int AppPort = 18081;
    private const int StatePort = 18082;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("payment_provider_verification")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3.13-alpine").Build();

    private Process _process = null!;
    private HttpListener _stateListener = null!;
    private CancellationTokenSource _stateLoop = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

        await using (var db = CreateDbContext())
            await db.Database.MigrateAsync();

        var rabbitUri = new Uri(_rabbit.GetConnectionString());
        var connectionFactory = new ConnectionFactory { Uri = rabbitUri };
        await using (var connection = await connectionFactory.CreateConnectionAsync())
        await using (var channel = await connection.CreateChannelAsync())
        {
            await channel.QueueDeclareAsync(SagaTopology.ChargePaymentQueue,
                durable: true, exclusive: false, autoDelete: false);
        }

        StartProviderStateListener();

        var dllPath = Path.Combine(AppContext.BaseDirectory, "PaymentService.dll");
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
                ["RabbitMq__HostName"] = rabbitUri.Host,
                ["RabbitMq__Port"] = rabbitUri.Port.ToString(),
                ["RabbitMq__UserName"] = "rabbitmq",
                ["RabbitMq__Password"] = "rabbitmq",
                ["RabbitMq__VirtualHost"] = "/",
                ["Payment__SimulateFailure"] = "false",
                ["Otel__Endpoint"] = "http://127.0.0.1:1",
            },
        };
        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PaymentService");

        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
                throw new InvalidOperationException(
                    $"PaymentService exited early (code {_process.ExitCode}):\n{await _process.StandardError.ReadToEndAsync()}");
            try
            {
                var probe = await http.GetAsync($"http://127.0.0.1:{AppPort}/api/internal/payments/stats");
                if (probe.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(500);
        }
        throw new TimeoutException("PaymentService never became ready");
    }

    public async Task DisposeAsync()
    {
        await _stateLoop.CancelAsync();
        _stateListener.Close();

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        _process.Dispose();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }

    private PaymentDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private void StartProviderStateListener()
    {
        _stateListener = new HttpListener();
        _stateListener.Prefixes.Add($"http://127.0.0.1:{StatePort}/");
        _stateListener.Start();
        _stateLoop = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            while (!_stateLoop.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _stateListener.GetContextAsync(); }
                catch (HttpListenerException) { return; }
                catch (ObjectDisposedException) { return; }

                try
                {
                    using var reader = new StreamReader(ctx.Request.InputStream);
                    var state = JsonDocument.Parse(await reader.ReadToEndAsync())
                        .RootElement.GetProperty("state").GetString();
                    await ApplyProviderStateAsync(state);
                    ctx.Response.StatusCode = 200;
                }
                catch
                {
                    ctx.Response.StatusCode = 500;
                }
                finally
                {
                    ctx.Response.Close();
                }
            }
        });
    }

    private async Task ApplyProviderStateAsync(string? state)
    {
        await using var db = CreateDbContext();
        await db.Payments.ExecuteDeleteAsync();

        if (state is null || !state.Contains("10 payments")) return;

        for (var i = 0; i < 10; i++)
        {
            db.Payments.Add(new Payment
            {
                BookingId = 1000 + i,
                Amount = 25m,
                Status = i < 4 ? PaymentStatus.Failed : PaymentStatus.Charged,
                CorrelationId = Guid.NewGuid(),
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public void PaymentService_SatisfiesGuestScoreServiceContract()
    {
        var pactPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "pacts", "GuestScoreService-PaymentService.json");

        new PactVerifier(nameof(PaymentServiceProviderVerificationTests))
            .WithHttpEndpoint(new Uri($"http://127.0.0.1:{AppPort}"))
            .WithFileSource(new FileInfo(pactPath))
            .WithProviderStateUrl(new Uri($"http://127.0.0.1:{StatePort}/provider-states"))
            .Verify();
    }
}