using HotelService.Application.DTOs;
using Shared.Models;

namespace HotelService.Application.Services;

public interface IRoomsService
{
    Task<IEnumerable<Room>> GetAllRoomsAsync();
    Task<IEnumerable<Room>> GetRoomsByHotelIdAsync(int hotelId);
    Task<Room?> GetRoomByIdAsync(int id);
    Task<Room> CreateRoomAsync(CreateRoomDto dto);
    Task<Room?> UpdateRoomAsync(int id, UpdateRoomDto dto);
    Task<bool> DeleteRoomAsync(int id);
    Task<bool> CheckAvailabilityAsync(int id, DateTime checkIn, DateTime checkOut);
    Task<bool> SetAvailabilityAsync(int id, bool isAvailable);
}
