using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Shared.Messaging;

public sealed class MessageHostedService(
    IConnection connection,
    IEnumerable<IMessageHandlerFactory> factories,
    ILogger<MessageHostedService> logger) : IHostedService, IDisposable
{
    private readonly List<(IChannel Channel, string ConsumerTag)> _consumers = [];

    private int _inFlight;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var factory in factories)
        {
            var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                Interlocked.Increment(ref _inFlight);
                try
                {
                    var message = JsonSerializer.Deserialize(ea.Body.ToArray(), factory.MessageType)
                        ?? throw new InvalidOperationException($"Null payload for {factory.MessageType.Name}");

                    await factory.HandleAsync(message, ea.BasicProperties, CancellationToken.None);
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process message from '{Queue}' — sending to DLQ", factory.QueueName);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: CancellationToken.None);
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlight);
                }
            };

            var consumerTag = await channel.BasicConsumeAsync(factory.QueueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
            _consumers.Add((channel, consumerTag));
            logger.LogInformation("Subscribed to queue '{Queue}' ({MessageType})", factory.QueueName, factory.MessageType.Name);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var (channel, consumerTag) in _consumers)
        {
            try
            {
                await channel.BasicCancelAsync(consumerTag, cancellationToken: CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cancel consumer '{ConsumerTag}' during shutdown", consumerTag);
            }
        }

        if (Volatile.Read(ref _inFlight) > 0)
            logger.LogInformation("Draining {Count} in-flight message(s) before shutdown", _inFlight);

        while (Volatile.Read(ref _inFlight) > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Shutdown timeout elapsed with {Count} message(s) still in flight; closing channels -s they will be redelivered", _inFlight);
                break;
            }

            await Task.Delay(50, CancellationToken.None);
        }

        Dispose();
    }

    public void Dispose()
    {
        foreach (var (channel, _) in _consumers.Where(c => c.Channel.IsOpen))
            channel.Dispose();
        _consumers.Clear();
    }
}