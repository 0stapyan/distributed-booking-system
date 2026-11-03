using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Shared.Messaging;

internal sealed class MessageHandlerFactory<TMessage> : IMessageHandlerFactory where TMessage : class
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MessageHandlerFactory(string queueName, IServiceScopeFactory scopeFactory)
    {
        QueueName = queueName;
        _scopeFactory = scopeFactory;
    }

    public string QueueName { get; }
    public Type MessageType => typeof(TMessage);

    public async Task HandleAsync(object message, IReadOnlyBasicProperties properties, CancellationToken cancellationToken)
    {
        if (message is not TMessage typedMessage)
            throw new ArgumentException($"Expected {typeof(TMessage).Name}", nameof(message));

        var traceParent = GetHeader(properties.Headers, "traceparent");
        var traceState = GetHeader(properties.Headers, "tracestate");
        var correlationId = GetHeader(properties.Headers, "x-correlation-id") ?? properties.CorrelationId;

        ActivityContext parentContext = default;
        if (traceParent is not null)
            ActivityContext.TryParse(traceParent, traceState, isRemote: true, out parentContext);

        using var activity = RabbitMqPublisher.ActivitySource.StartActivity("rabbitmq process", ActivityKind.Consumer, parentContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", QueueName);
        if (correlationId is not null)
            activity?.SetTag("x-correlation-id", correlationId);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var accessor = scope.ServiceProvider.GetRequiredService<IMessageIdAccessor>();
        if (properties.MessageId is not null && Guid.TryParse(properties.MessageId, out var msgId))
            accessor.SetMessageId(msgId);
        if (correlationId is not null)
            accessor.SetCorrelationId(correlationId);
        if (properties.Headers is not null)
            accessor.SetHeaders(properties.Headers);

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MessageHandlerFactory<TMessage>>>();
        logger.LogInformation("Handling {MessageType} from queue '{Queue}'", typeof(TMessage).Name, QueueName);

        var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<TMessage>>();
        try
        {
            await handler.HandleAsync(typedMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private static string? GetHeader(IDictionary<string, object?>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var raw)) return null;
        return raw switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string s => s,
            _ => null,
        };
    }
}