using System.Diagnostics.Metrics;

namespace BookingService.Saga;

public sealed class SagaMetrics : IDisposable
{
    public const string MeterName = "BookingService.Saga";

    private readonly Meter _meter;
    private readonly Counter<long> _started;
    private readonly Counter<long> _completed;
    private readonly Histogram<double> _stepDuration;
    private readonly Counter<long> _compensations;
    private volatile int _active;

    public SagaMetrics()
    {
        _meter = new Meter(MeterName);

        _started = _meter.CreateCounter<long>(
            "saga_started_total",
            description: "Total sagas started");

        _completed = _meter.CreateCounter<long>(
            "saga_completed_total",
            description: "Total sagas completed (completed|compensated|failed)");

        _stepDuration = _meter.CreateHistogram<double>(
            "saga_step_duration_seconds",
            unit: "s",
            description: "Real-world duration of each saga step (command dispatch → event receipt)");

        _compensations = _meter.CreateCounter<long>(
            "saga_compensations_total",
            description: "Total compensation actions triggered");

        _meter.CreateObservableGauge<int>(
            "saga_active",
            () => _active,
            description: "Number of sagas currently in flight");

        // Initialize counters so they appear in /metrics even at zero
        var team = Tag("team", "team-06");
        var sagaType = Tag("saga_type", "booking");
        _started.Add(0, team, sagaType);
        _completed.Add(0, team, sagaType, Tag("outcome", "completed"));
        _completed.Add(0, team, sagaType, Tag("outcome", "compensated"));
        _completed.Add(0, team, sagaType, Tag("outcome", "failed"));
        _compensations.Add(0, team, sagaType, Tag("step", "charge-payment"));
        _compensations.Add(0, team, sagaType, Tag("step", "reserve-room"));
    }

    public void RecordStarted() =>
        _started.Add(1, Tag("team", "team-06"), Tag("saga_type", "booking"));

    public void RecordCompleted(string outcome) =>
        _completed.Add(1, Tag("team", "team-06"), Tag("saga_type", "booking"), Tag("outcome", outcome));

    public void RecordStepDuration(double seconds, string step) =>
        _stepDuration.Record(seconds, Tag("team", "team-06"), Tag("saga_type", "booking"), Tag("step", step));

    public void RecordCompensation(string step) =>
        _compensations.Add(1, Tag("team", "team-06"), Tag("saga_type", "booking"), Tag("step", step));

    public void IncrementActive() => Interlocked.Increment(ref _active);
    public void DecrementActive() => Interlocked.Decrement(ref _active);

    public void Dispose() => _meter.Dispose();

    private static KeyValuePair<string, object?> Tag(string k, string v) => new(k, v);
}
