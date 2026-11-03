using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Cdc;

public static class CdcExtensions
{
    public static IHostApplicationBuilder AddKafkaCdc(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));
        return builder;
    }

    public static IHostApplicationBuilder AddCdcProjector<TProjector>(
        this IHostApplicationBuilder builder,
        string topic,
        string groupId)
        where TProjector : class, ICdcProjector
    {
        builder.Services.AddScoped<TProjector>();
        builder.Services.AddSingleton<IHostedService>(sp =>
            new CdcConsumerService(
                topic,
                groupId,
                typeof(TProjector),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IOptions<KafkaSettings>>(),
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<CdcConsumerService>()));
        return builder;
    }
}