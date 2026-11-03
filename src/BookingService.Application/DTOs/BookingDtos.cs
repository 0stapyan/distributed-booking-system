using Shared.Models;

namespace BookingService.Application.DTOs;

public record CreateBookingDto(
    List<int> GuestIds,
    int RoomId,
    DateTime CheckInDate,
    DateTime CheckOutDate
);

public record UpdateBookingStatusDto(BookingStatus Status);
