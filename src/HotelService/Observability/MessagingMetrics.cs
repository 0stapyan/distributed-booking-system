using System.Diagnostics.Metrics;

namespace HotelService.Observability;

public sealed class MessagingMetrics : IDisposable
{
    public const string MeterName = "HotelService.Messaging";

    private readonly Meter _meter;
    private readonly Counter<long> _messagesPublished;
    private readonly Counter<long> _messagesConsumed;
    private readonly Histogram<double> _processingDuration;
    private readonly Counter<long> _deadLettered;
    private volatile int _pendingCount;

    public MessagingMetrics()
    {
        _meter = new Meter(MeterName);

        _messagesPublished = _meter.CreateCounter<long>(
            "messages_published_total",
            description: "Total messages published to RabbitMQ");

        _messagesConsumed = _meter.CreateCounter<long>(
            "messages_consumed_total",
            description: "Total messages consumed from RabbitMQ");

        _processingDuration = _meter.CreateHistogram<double>(
            "messages_processing_duration_seconds",
            unit: "s",
            description: "Time spent publishing or consuming a message");

        _meter.CreateObservableGauge<int>(
            "outbox_pending_messages",
            () => _pendingCount,
            description: "Number of unpublished outbox messages");

        _deadLettered = _meter.CreateCounter<long>(
            "messages_dead_lettered_total",
            description: "Total messages routed to DLQ");

        // Initialize counters so they appear in /metrics output even with zero values
        var baseQueue = new KeyValuePair<string, object?>("queue", "team-06.hotels.hotel-created");
        var team = new KeyValuePair<string, object?>("team", "team-06");
        var svc = new KeyValuePair<string, object?>("service", "hotel-service");
        _messagesConsumed.Add(0, team, svc, baseQueue, new KeyValuePair<string, object?>("status", "success"));
        _deadLettered.Add(0, team, svc, baseQueue);
    }

    public void RecordPublished(string queue, string status) =>
        _messagesPublished.Add(1,
            new KeyValuePair<string, object?>("team", "team-06"),
            new KeyValuePair<string, object?>("service", "hotel-service"),
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("status", status));

    public void RecordConsumed(string queue, string status) =>
        _messagesConsumed.Add(1,
            new KeyValuePair<string, object?>("team", "team-06"),
            new KeyValuePair<string, object?>("service", "hotel-service"),
            new KeyValuePair<string, object?>("queue", queue),
            new KeyValuePair<string, object?>("status", status));

    public void RecordProcessingDuration(double seconds, string queue) =>
        _processingDuration.Record(seconds,
            new KeyValuePair<string, object?>("team", "team-06"),
            new KeyValuePair<string, object?>("service", "hotel-service"),
            new KeyValuePair<string, object?>("queue", queue));

    public void SetPendingCount(int count) => _pendingCount = count;

    public void RecordDeadLettered(string queue) =>
        _deadLettered.Add(1,
            new KeyValuePair<string, object?>("team", "team-06"),
            new KeyValuePair<string, object?>("service", "hotel-service"),
            new KeyValuePair<string, object?>("queue", queue));

    public void Dispose() => _meter.Dispose();
}
