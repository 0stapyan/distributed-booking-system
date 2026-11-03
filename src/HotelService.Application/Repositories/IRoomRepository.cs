using Shared.Models;

namespace HotelService.Application.Repositories;

public interface IRoomRepository
{
    Task<IEnumerable<Room>> GetAllAsync();
    Task<IEnumerable<Room>> GetByHotelIdAsync(int hotelId);
    Task<Room?> GetByIdAsync(int id);
    Task<Room> CreateAsync(Room room);
    Task<Room?> UpdateAsync(int id, Room room);
    Task<bool> DeleteAsync(int id);
    Task<bool> SetAvailabilityAsync(int id, bool isAvailable);
}
