using HotelService.Application.DTOs;
using Shared.Models;

namespace HotelService.Application.Services;

public interface IHotelsService
{
    Task<IEnumerable<Hotel>> GetAllHotelsAsync();
    Task<Hotel?> GetHotelByIdAsync(int id);
    Task<Hotel> CreateHotelAsync(CreateHotelDto dto);
    Task<Hotel?> UpdateHotelAsync(int id, UpdateHotelDto dto);
    Task<bool> DeleteHotelAsync(int id);
}
