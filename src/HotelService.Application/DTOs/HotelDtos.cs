using Shared.Models;

namespace HotelService.Application.DTOs;

public record CreateHotelDto(
    string Name,
    string Address,
    string City,
    int StarRating,
    string Description
);

public record UpdateHotelDto(
    string Name,
    string Address,
    string City,
    int StarRating,
    string Description
);

public record CreateRoomDto(
    int HotelId,
    string RoomNumber,
    RoomType Type,
    decimal PricePerNight,
    int MaxGuests
);

public record UpdateRoomDto(
    string RoomNumber,
    RoomType Type,
    decimal PricePerNight,
    int MaxGuests,
    bool IsAvailable
);
