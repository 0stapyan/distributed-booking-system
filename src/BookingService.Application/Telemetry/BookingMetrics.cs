using System.Diagnostics.Metrics;

namespace BookingService.Application.Telemetry;

public class BookingMetrics
{
    public const string MeterName = "BookingService";
    private readonly Counter<int> _bookingsCreated;
    private readonly Histogram<decimal> _bookingTotalPrice;
    public BookingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _bookingsCreated = meter.CreateCounter<int>("bookings_created_total", "bookings", "Total number of bookings created");
        _bookingTotalPrice = meter.CreateHistogram<decimal>("booking_total_price", "UAH", "Total price of created bookings");
    }
    public void RecordBookingCreated(int roomId) =>
        _bookingsCreated.Add(1, new KeyValuePair<string, object?>("room_id", roomId));

    public void RecordBookingPrice(decimal price) =>
        _bookingTotalPrice.Record(price);
}
