using GuestService.Application.DTOs;
using Shared.Models;

namespace GuestService.Application.Services;

public interface IGuestsService
{
    Task<IEnumerable<Guest>> GetAllGuestsAsync();
    Task<Guest?> GetGuestByIdAsync(int id);
    Task<Guest?> GetGuestByEmailAsync(string email);
    Task<Guest> CreateGuestAsync(CreateGuestDto dto);
    Task<Guest?> UpdateGuestAsync(int id, UpdateGuestDto dto);
    Task<bool> DeleteGuestAsync(int id);
}
