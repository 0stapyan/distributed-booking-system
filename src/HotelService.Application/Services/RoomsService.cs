using HotelService.Application.DTOs;
using HotelService.Application.Repositories;
using Shared.Models;

namespace HotelService.Application.Services;

public class RoomsService : IRoomsService
{
    private readonly IRoomRepository _roomRepository;

    public RoomsService(IRoomRepository roomRepository)
    {
        _roomRepository = roomRepository;
    }

    public Task<IEnumerable<Room>> GetAllRoomsAsync() =>
        _roomRepository.GetAllAsync();

    public Task<IEnumerable<Room>> GetRoomsByHotelIdAsync(int hotelId) =>
        _roomRepository.GetByHotelIdAsync(hotelId);

    public Task<Room?> GetRoomByIdAsync(int id) =>
        _roomRepository.GetByIdAsync(id);

    public Task<Room> CreateRoomAsync(CreateRoomDto dto)
    {
        var room = new Room
        {
            HotelId = dto.HotelId,
            RoomNumber = dto.RoomNumber,
            Type = dto.Type,
            PricePerNight = dto.PricePerNight,
            MaxGuests = dto.MaxGuests,
            IsAvailable = true
        };
        return _roomRepository.CreateAsync(room);
    }

    public Task<Room?> UpdateRoomAsync(int id, UpdateRoomDto dto)
    {
        var room = new Room
        {
            RoomNumber = dto.RoomNumber,
            Type = dto.Type,
            PricePerNight = dto.PricePerNight,
            MaxGuests = dto.MaxGuests,
            IsAvailable = dto.IsAvailable
        };
        return _roomRepository.UpdateAsync(id, room);
    }

    public Task<bool> DeleteRoomAsync(int id) =>
        _roomRepository.DeleteAsync(id);

    public async Task<bool> CheckAvailabilityAsync(int id, DateTime checkIn, DateTime checkOut)
    {
        var room = await _roomRepository.GetByIdAsync(id);
        return room is not null && room.IsAvailable;
    }

    public Task<bool> SetAvailabilityAsync(int id, bool isAvailable) =>
        _roomRepository.SetAvailabilityAsync(id, isAvailable);
}
