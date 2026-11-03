using System.Diagnostics.Metrics;

namespace NotificationService.Observability;

public sealed class NotificationMetrics : IDisposable
{
    public const string MeterName = "NotificationService.Messaging";

    private readonly Meter _meter;
    private readonly Counter<long> _messagesConsumed;
    private readonly Counter<long> _deadLettered;
    private readonly Histogram<double> _processingDuration;

    public NotificationMetrics()
    {
        _meter = new Meter(MeterName);

        _messagesConsumed = _meter.CreateCounter<long>(
            "messages_consumed_total",
            description: "Total messages consumed from RabbitMQ");

        _deadLettered = _meter.CreateCounter<long>(
            "messages_dead_lettered_total",
            description: "Total messages routed to DLQ after max retries");

        _processingDuration = _meter.CreateHistogram<double>(
            "messages_processing_duration_seconds",
            unit: "s",
            description: "Time spent processing a consumed message");
    }

    public void RecordConsumed(string queue, string status) =>
        _messagesConsumed.Add(1,
            new KeyValuePair<string, object?>("team", "team-06"),
            new KeyValuePair<string, object?>("service", "notification-service"),
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("status", status));

    public void RecordDeadLettered(string queue) =>
        _deadLettered.Add(1,
            new KeyValuePair<string, object?>("team", "team-06"),
            new KeyValuePair<string, object?>("service", "notification-service"),
            new KeyValuePair<string, object?>("queue", queue));

    public void RecordProcessingDuration(double seconds, string queue) =>
        _processingDuration.Record(seconds,
            new KeyValuePair<string, object?>("team", "team-06"),
            new KeyValuePair<string, object?>("service", "notification-service"),
            new KeyValuePair<string, object?>("queue", queue));

    public void Dispose() => _meter.Dispose();
}
