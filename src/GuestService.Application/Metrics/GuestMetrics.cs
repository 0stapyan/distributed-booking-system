using System.Diagnostics.Metrics;

namespace GuestService.Application.Metrics;

public sealed class GuestMetrics : IGuestMetrics, IDisposable
{
    public const string MeterName = "GuestService";

    private readonly Meter _meter;
    private readonly Counter<long> _guestsCreated;
    private readonly Histogram<double> _creationDuration;

    public GuestMetrics()
    {
        _meter = new Meter(MeterName);

        _guestsCreated = _meter.CreateCounter<long>(
            "guests.created.total",
            unit: "{guests}",
            description: "Total guests successfully registered");

        _creationDuration = _meter.CreateHistogram<double>(
            "guests.creation_duration_ms",
            unit: "{ms}",
            description: "Time to create and persist a guest profile, in milliseconds");
    }

    public void GuestCreated() => _guestsCreated.Add(1);

    public void RecordCreationDuration(double milliseconds) => _creationDuration.Record(milliseconds);

    public void Dispose() => _meter.Dispose();
}