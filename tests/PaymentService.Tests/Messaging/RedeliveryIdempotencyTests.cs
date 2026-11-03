using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaymentService.Data;
using PaymentService.Models;
using PaymentService.Saga;
using RabbitMQ.Client;
using Shared.Messaging;
using Shared.Saga;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace PaymentService.Tests.Messaging;

public class RedeliveryIdempotencyTests : IAsyncLifetime
{
    private const string Queue = "test.charge-payment";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("paymentdb").WithUsername("payment").WithPassword("payment123").Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3.13-alpine").Build();

    private IHost _host = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = _rabbit.Hostname,
            ["RabbitMq:Port"] = _rabbit.GetMappedPublicPort(5672).ToString(),
            ["RabbitMq:UserName"] = "rabbitmq",
            ["RabbitMq:Password"] = "rabbitmq",
            ["RabbitMq:VirtualHost"] = "/",
            ["Payment:SimulateFailure"] = "false",
        });
        builder.Services.AddDbContext<PaymentDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        builder.AddRabbitMq();
        builder.AddScopedMessageHandler<ChargePaymentCommand, ChargePaymentHandler>(Queue);

        _host = builder.Build();

        await using var db = _host.Services.CreateScope().ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _host.Dispose();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbit.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Unacked_message_is_redelivered_and_reprocessing_is_idempotent()
    {
        var connection = _host.Services.GetRequiredService<IConnection>();
        var factory = _host.Services.GetServices<IMessageHandlerFactory>().Single();

        var command = new ChargePaymentCommand(Guid.NewGuid(), BookingId: 4242, Amount: 250m);
        var messageId = Guid.NewGuid();

        await using (var publishChannel = await connection.CreateChannelAsync())
        {
            await publishChannel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false);
            await publishChannel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: Queue,
                mandatory: false,
                basicProperties: new BasicProperties { MessageId = messageId.ToString(), ContentType = "application/json" },
                body: JsonSerializer.SerializeToUtf8Bytes(command));
        }

        await using (var channel = await connection.CreateChannelAsync())
        {
            var delivery = await channel.BasicGetAsync(Queue, autoAck: false);
            Assert.NotNull(delivery);
            Assert.False(delivery.Redelivered);

            await factory.HandleAsync(Decode(delivery.Body.ToArray()), delivery.BasicProperties, CancellationToken.None);
        }

        await AssertChargedExactlyOnceAsync();

        await using (var channel = await connection.CreateChannelAsync())
        {
            var redelivery = await channel.BasicGetAsync(Queue, autoAck: false);
            Assert.NotNull(redelivery);
            Assert.True(redelivery.Redelivered);
            Assert.Equal(messageId.ToString(), redelivery.BasicProperties.MessageId);

            await factory.HandleAsync(Decode(redelivery.Body.ToArray()), redelivery.BasicProperties, CancellationToken.None);
            await channel.BasicAckAsync(redelivery.DeliveryTag, multiple: false);
        }

        await AssertChargedExactlyOnceAsync();
    }

    private static ChargePaymentCommand Decode(byte[] body) =>
        JsonSerializer.Deserialize<ChargePaymentCommand>(Encoding.UTF8.GetString(body))!;

    private async Task AssertChargedExactlyOnceAsync()
    {
        using var scope = _host.Services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        var payments = await db.Payments.Where(p => p.BookingId == 4242).ToListAsync();
        Assert.Single(payments);
        Assert.Equal(PaymentStatus.Charged, payments[0].Status);
        Assert.Equal(1, await db.InboxMessages.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
    }
}