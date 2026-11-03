using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using RabbitMQ.Client;

namespace Shared.Messaging;

public static class MessagingExtensions
{
    public static IHostApplicationBuilder AddRabbitMq(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));

        builder.Services.AddSingleton<IConnection>(sp =>
        {
            var s = sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
            var factory = new ConnectionFactory
            {
                HostName = s.HostName,
                Port = s.Port,
                UserName = s.UserName,
                Password = s.Password,
                VirtualHost = s.VirtualHost,
            };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        // Dedicated channel for publishing (consumers create their own channels via MessageHostedService).
        builder.Services.AddSingleton<IChannel>(sp =>
            sp.GetRequiredService<IConnection>().CreateChannelAsync().GetAwaiter().GetResult());

        builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        builder.Services.AddScoped<IMessageIdAccessor, MessageIdAccessor>();

        // Register the RabbitMQ activity source so OTel picks up publish/consume spans.
        builder.Services.AddOpenTelemetry()
            .WithTracing(t => t.AddSource("rabbitmq"));

        return builder;
    }

    public static IHostApplicationBuilder AddMessageHandler<TMessage, THandler>(
        this IHostApplicationBuilder builder, string queueName)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MessageHostedService>());
        builder.Services.AddSingleton<IMessageHandlerFactory, MessageHandlerFactory<TMessage>>(sp =>
            new MessageHandlerFactory<TMessage>(queueName, sp.GetRequiredService<IServiceScopeFactory>()));
        builder.Services.AddSingleton<IMessageHandler<TMessage>, THandler>();
        return builder;
    }

    public static IHostApplicationBuilder AddScopedMessageHandler<TMessage, THandler>(
        this IHostApplicationBuilder builder, string queueName)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MessageHostedService>());
        builder.Services.AddSingleton<IMessageHandlerFactory, MessageHandlerFactory<TMessage>>(sp =>
            new MessageHandlerFactory<TMessage>(queueName, sp.GetRequiredService<IServiceScopeFactory>()));
        builder.Services.AddScoped<IMessageHandler<TMessage>, THandler>();
        return builder;
    }
}
