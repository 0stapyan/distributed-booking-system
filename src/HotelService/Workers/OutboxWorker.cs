using System.Diagnostics;
using HotelService.Observability;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Messaging;

namespace HotelService.Workers;

public class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxWorker> _logger;
    private readonly MessagingMetrics _metrics;

    private const string DefaultExchange = "team-06.hotels";
    private const string Queue = "team-06.hotels.hotel-created";

    public OutboxWorker(
        IServiceScopeFactory scopeFactory,
        IMessagePublisher publisher,
        ILogger<OutboxWorker> logger,
        MessagingMetrics metrics)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
        _metrics = metrics;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[outbox] Worker poll failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var pending = await db.OutboxMessages
            .FromSqlRaw("""
                SELECT * FROM "OutboxMessages"
                WHERE "PublishedAt" IS NULL
                ORDER BY "CreatedAt"
                LIMIT 10
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        _metrics.SetPendingCount(pending.Count);

        if (pending.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        _logger.LogInformation("[outbox] Publishing {Count} pending message(s)", pending.Count);

        foreach (var message in pending)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await _publisher.PublishRawAsync(
                    message.EventType,
                    message.Exchange ?? DefaultExchange,
                    message.Payload,
                    message.CorrelationId,
                    message.TraceParent,
                    traceState: null);

                message.PublishedAt = DateTime.UtcNow;
                sw.Stop();
                _metrics.RecordPublished(Queue, "success");
                _metrics.RecordProcessingDuration(sw.Elapsed.TotalSeconds, Queue);
                _logger.LogInformation("[outbox] Published {EventType} id={Id}", message.EventType, message.Id);
            }
            catch (Exception ex)
            {
                sw.Stop();
                message.RetryCount++;
                _metrics.RecordPublished(Queue, "failure");
                _metrics.RecordProcessingDuration(sw.Elapsed.TotalSeconds, Queue);
                _logger.LogWarning(ex, "[outbox] Failed to publish id={Id} retries={RetryCount}", message.Id, message.RetryCount);
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}