namespace Shared.Saga;

public static class SagaTopology
{
    public const string Exchange = "team-06.saga";

    public const string ReserveRoomKey = "reserve-room";
    public const string RoomReservedKey = "room-reserved";
    public const string ChargePaymentKey = "charge-payment";
    public const string PaymentChargedKey = "payment-charged";
    public const string BookingConfirmedKey = "booking-confirmed";

    public const string RoomReservationFailedKey = "room-reservation-failed";
    public const string PaymentFailedKey = "payment-failed";

    public const string ReleaseRoomKey = "release-room";
    public const string RoomReleasedKey = "room-released";
    public const string BookingCancelledKey = "booking-cancelled";

    public const string ReserveRoomQueue = "team-06.saga.reserve-room";
    public const string RoomReservedQueue = "team-06.saga.room-reserved";
    public const string ChargePaymentQueue = "team-06.saga.charge-payment";
    public const string PaymentChargedQueue = "team-06.saga.payment-charged";
    public const string BookingConfirmedQueue = "team-06.saga.booking-confirmed";

    public const string RoomReservationFailedQueue = "team-06.saga.room-reservation-failed";
    public const string PaymentFailedQueue = "team-06.saga.payment-failed";

    public const string ReleaseRoomQueue = "team-06.saga.release-room";
    public const string RoomReleasedQueue = "team-06.saga.room-released";
    public const string BookingCancelledQueue = "team-06.saga.booking-cancelled";
}