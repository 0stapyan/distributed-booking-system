using Shared.Models;

namespace HotelService.Application.Repositories;

public interface IHotelRepository
{
    Task<IEnumerable<Hotel>> GetAllAsync();
    Task<Hotel?> GetByIdAsync(int id);
    Task<Hotel> CreateAsync(Hotel hotel);
    Task<Hotel?> UpdateAsync(int id, Hotel hotel);
    Task<bool> DeleteAsync(int id);
}
