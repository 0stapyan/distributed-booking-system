using Shared.Models;

namespace GuestService.Application.Repositories;

public interface IGuestRepository
{
    Task<IEnumerable<Guest>> GetAllAsync();
    Task<Guest?> GetByIdAsync(int id);
    Task<Guest?> GetByEmailAsync(string email);
    Task<Guest> CreateAsync(Guest guest);
    Task<Guest?> UpdateAsync(int id, Guest guest);
    Task<bool> DeleteAsync(int id);
}
