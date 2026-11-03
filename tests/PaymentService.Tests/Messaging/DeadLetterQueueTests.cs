using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Shared.Messaging;
using Shared.Saga;
using Testcontainers.RabbitMq;
using Xunit;

namespace PaymentService.Tests.Messaging;

public class DeadLetterQueueTests : IAsyncLifetime
{
    private const string Queue = "test.poison";
    private const string DeadLetterExchange = "test.poison.dlx";
    private const string DeadLetterQueue = "test.poison.dlq";

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3.13-alpine").Build();

    private IHost _host = null!;

    public sealed class AlwaysThrowsHandler : IMessageHandler<ChargePaymentCommand>
    {
        public static int Invocations;

        public Task HandleAsync(ChargePaymentCommand message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Invocations);
            throw new InvalidOperationException("poison message");
        }
    }

    public async Task InitializeAsync()
    {
        await _rabbit.StartAsync();
        AlwaysThrowsHandler.Invocations = 0;

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = _rabbit.Hostname,
            ["RabbitMq:Port"] = _rabbit.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:UserName"] = "rabbitmq",
            ["RabbitMq:Password"] = "rabbitmq",
            ["RabbitMq:VirtualHost"] = "/",
        });
        builder.AddRabbitMq();
        builder.AddScopedMessageHandler<ChargePaymentCommand, AlwaysThrowsHandler>(Queue);
        _host = builder.Build();

        var connection = _host.Services.GetRequiredService<IConnection>();
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Fanout, durable: true);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, routingKey: string.Empty);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange });
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _rabbit.DisposeAsync();
    }

    [Fact]
    public async Task Poison_message_lands_in_the_dlq_instead_of_looping_forever()
    {
        var command = new ChargePaymentCommand(Guid.NewGuid(), BookingId: 999, Amount: 10m);

        var connection = _host.Services.GetRequiredService<IConnection>();
        await using (var publishChannel = await connection.CreateChannelAsync())
        {
            await publishChannel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: Queue,
                mandatory: false,
                basicProperties: new BasicProperties { MessageId = Guid.NewGuid().ToString(), ContentType = "application/json" },
                body: JsonSerializer.SerializeToUtf8Bytes(command));
        }

        await _host.StartAsync();

        BasicGetResult? dead = null;
        await using (var channel = await connection.CreateChannelAsync())
        {
            for (var i = 0; i < 50 && dead is null; i++)
            {
                dead = await channel.BasicGetAsync(DeadLetterQueue, autoAck: true);
                if (dead is null) await Task.Delay(100);
            }
        }

        Assert.NotNull(dead);
        Assert.Equal(command.CorrelationId, JsonSerializer.Deserialize<ChargePaymentCommand>(dead.Body.Span)!.CorrelationId);

        Assert.Equal(1, AlwaysThrowsHandler.Invocations);

        await using (var channel = await connection.CreateChannelAsync())
        {
            var stillQueued = await channel.MessageCountAsync(Queue);
            Assert.Equal(0u, stillQueued);
        }
    }
}