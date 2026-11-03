namespace Shared.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, string routingKey, string? exchange = null,
        string? correlationId = null, string? traceParent = null, string? traceState = null);

    Task PublishRawAsync(string routingKey, string? exchange, string jsonPayload,
        string? correlationId, string? traceParent, string? traceState);
}
