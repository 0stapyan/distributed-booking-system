using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Shared.Messaging;

public sealed class RabbitMqPublisher(IChannel channel, ILogger<RabbitMqPublisher> logger) : IMessagePublisher
{
    internal static readonly ActivitySource ActivitySource = new("rabbitmq");

    public Task PublishAsync<T>(T message, string routingKey, string? exchange = null,
        string? correlationId = null, string? traceParent = null, string? traceState = null)
        => PublishRawAsync(routingKey, exchange, JsonSerializer.Serialize(message), correlationId, traceParent, traceState);

    public async Task PublishRawAsync(string routingKey, string? exchange, string jsonPayload,
        string? correlationId, string? traceParent, string? traceState)
    {
        ActivityContext parentContext = default;
        if (traceParent is not null)
            ActivityContext.TryParse(traceParent, traceState, isRemote: true, out parentContext);

        using var activity = ActivitySource.StartActivity("rabbitmq publish", ActivityKind.Producer, parentContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", routingKey);
        activity?.SetTag("messaging.destination_kind", exchange is null ? "queue" : "exchange");

        // Inject the publish span's traceparent so the consumer can link to it.
        var headers = new Dictionary<string, object?>();
        if (activity?.Id is { Length: > 0 } id) headers["traceparent"] = id;
        if (activity?.TraceStateString is { Length: > 0 } ts) headers["tracestate"] = ts;
        if (correlationId is not null) headers["x-correlation-id"] = correlationId;

        var props = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = correlationId,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = headers,
        };

        var body = Encoding.UTF8.GetBytes(jsonPayload);
        await channel.BasicPublishAsync(exchange ?? string.Empty, routingKey, mandatory: false, basicProperties: props, body: body);

        logger.LogDebug("Published to {Exchange}/{RoutingKey} ({Bytes} bytes)", exchange ?? "(default)", routingKey, body.Length);
    }
}
