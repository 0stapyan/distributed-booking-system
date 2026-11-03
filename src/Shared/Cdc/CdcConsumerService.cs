using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Cdc;

public sealed class CdcConsumerService : BackgroundService
{
    private readonly string _topic;
    private readonly string _groupId;
    private readonly Type _projectorType;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaSettings _settings;
    private readonly ILogger<CdcConsumerService> _logger;

    public CdcConsumerService(
        string topic,
        string groupId,
        Type projectorType,
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaSettings> settings,
        ILogger<CdcConsumerService> logger)
    {
        _topic = topic;
        _groupId = groupId;
        _projectorType = projectorType;
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.BootstrapServers))
        {
            _logger.LogWarning(
                "Kafka:BootstrapServers not configured — CDC projector {Projector} for topic {Topic} is disabled",
                _projectorType.Name, _topic);
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string?, string?>(config).Build();
        consumer.Subscribe(_topic);

        _logger.LogInformation("CDC consumer started: topic={Topic} group={Group} projector={Projector}",
            _topic, _groupId, _projectorType.Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string?, string?>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);

                if (result.IsPartitionEOF || result.Message?.Value is null)
                {
                    if (result.Message is not null)
                        consumer.Commit(result);
                    continue;
                }

                using var doc = JsonDocument.Parse(result.Message.Value);
                var root = doc.RootElement;

                var lsn = root.TryGetProperty("__lsn", out var lsnEl) ? lsnEl.GetInt64() : 0L;
                var deleted = root.TryGetProperty("__deleted", out var delEl)
                    && (delEl.ValueKind == JsonValueKind.True
                        || (delEl.ValueKind == JsonValueKind.String && delEl.GetString() == "true"));

                using var scope = _scopeFactory.CreateAsyncScope();
                var projector = (ICdcProjector)scope.ServiceProvider.GetRequiredService(_projectorType);
                await projector.ProjectAsync(root, lsn, deleted, stoppingToken);

                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CDC message from topic {Topic} offset {Offset}",
                    _topic, result?.Offset);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        consumer.Close();
        _logger.LogInformation("CDC consumer stopped: topic={Topic}", _topic);
    }
}