using RabbitMQ.Client;

namespace Shared.Messaging;

public interface IMessageHandlerFactory
{
    string QueueName { get; }
    Type MessageType { get; }
    Task HandleAsync(object message, IReadOnlyBasicProperties properties, CancellationToken cancellationToken);
}
