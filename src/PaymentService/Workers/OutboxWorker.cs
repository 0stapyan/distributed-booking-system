using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using Shared.Messaging;
using Shared.Saga;

namespace PaymentService.Workers;

public class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxWorker> _logger;

    private const string DefaultExchange = SagaTopology.Exchange;

    public OutboxWorker(IServiceScopeFactory scopeFactory, IMessagePublisher publisher, ILogger<OutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _logger = logger;
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
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var messages = await db.OutboxMessages
            .FromSqlRaw("""
                SELECT * FROM "OutboxMessages"
                WHERE "PublishedAt" IS NULL
                ORDER BY "CreatedAt"
                LIMIT 10
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        if (messages.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return;
        }

        _logger.LogInformation("[outbox] Publishing {Count} pending message(s)", messages.Count);

        foreach (var message in messages)
        {
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
                _logger.LogInformation("[outbox] Published {EventType} id={Id}", message.EventType, message.Id);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                _logger.LogWarning(ex, "[outbox] Failed to publish id={Id} retries={RetryCount}", message.Id, message.RetryCount);
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
