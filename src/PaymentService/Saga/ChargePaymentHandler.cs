using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Models;
using Shared.Data;
using Shared.Messaging;
using Shared.Saga;

namespace PaymentService.Saga;

public sealed class ChargePaymentHandler : IMessageHandler<ChargePaymentCommand>
{
    private readonly PaymentDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMessageIdAccessor _accessor;
    private readonly ILogger<ChargePaymentHandler> _logger;

    public ChargePaymentHandler(PaymentDbContext db, IConfiguration configuration, IMessageIdAccessor accessor, ILogger<ChargePaymentHandler> logger)
    {
        _db = db;
        _configuration = configuration;
        _accessor = accessor;
        _logger = logger;
    }

    public async Task HandleAsync(ChargePaymentCommand message, CancellationToken cancellationToken)
    {
        var messageId = _accessor.GetMessageId();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (await _db.InboxMessages.AnyAsync(m => m.Id == messageId, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogInformation("[idempotency] Duplicate ChargePaymentCommand messageId={MessageId} — skipped", messageId);
            return;
        }

        var traceParent = Activity.Current?.Id;
        var simulateFailure = _configuration.GetValue<bool>("Payment:SimulateFailure");

        if (simulateFailure)
        {
            const string reason = "Simulated payment gateway failure";

            _db.Payments.Add(new Payment
            {
                BookingId = message.BookingId,
                Amount = message.Amount,
                Status = PaymentStatus.Failed,
                CorrelationId = message.CorrelationId,
            });

            _db.OutboxMessages.Add(new OutboxMessage
            {
                EventType = SagaTopology.PaymentFailedKey,
                Exchange = SagaTopology.Exchange,
                Payload = JsonSerializer.Serialize(new PaymentFailedEvent(message.CorrelationId, message.BookingId, reason)),
                CorrelationId = message.CorrelationId.ToString(),
                CreatedAt = DateTime.UtcNow,
                TraceParent = traceParent,
            });

            _db.InboxMessages.Add(new InboxMessage { Id = messageId, MessageType = nameof(ChargePaymentCommand) });

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            _logger.LogWarning("[saga] {CorrelationId} payment FAILED (simulated) for booking {BookingId}", message.CorrelationId, message.BookingId);
            return;
        }

        var payment = new Payment
        {
            BookingId = message.BookingId,
            Amount = message.Amount,
            Status = PaymentStatus.Charged,
            CorrelationId = message.CorrelationId,
        };
        _db.Payments.Add(payment);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = SagaTopology.PaymentChargedKey,
            Exchange = SagaTopology.Exchange,
            Payload = JsonSerializer.Serialize(new PaymentChargedEvent(message.CorrelationId, message.BookingId, payment.Id, message.Amount)),
            CorrelationId = message.CorrelationId.ToString(),
            CreatedAt = DateTime.UtcNow,
            TraceParent = traceParent,
        });

        _db.InboxMessages.Add(new InboxMessage { Id = messageId, MessageType = nameof(ChargePaymentCommand) });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation("[saga] {CorrelationId} charged {Amount} for booking {BookingId}, payment {PaymentId}", message.CorrelationId, message.Amount, message.BookingId, payment.Id);
    }
}