using System.Diagnostics.Metrics;

namespace HotelService.Observability;

public sealed class HotelMetrics : IDisposable
{
    public const string MeterName = "HotelService";

    private readonly Meter _meter;

    public readonly Counter<long> HotelsCreated;

    public readonly Counter<long> RoomsCreated;

    public readonly Histogram<double> RoomPricePerNight;

    public HotelMetrics()
    {
        _meter = new Meter(MeterName);

        HotelsCreated = _meter.CreateCounter<long>(
            "hotels.created.total",
            description: "Total number of hotels created");

        RoomsCreated = _meter.CreateCounter<long>(
            "rooms.created.total",
            description: "Total number of rooms created");

        RoomPricePerNight = _meter.CreateHistogram<double>(
            "rooms.price_per_night",
            unit: "USD",
            description: "Distribution of room prices per night");
    }

    public void Dispose() => _meter.Dispose();
}