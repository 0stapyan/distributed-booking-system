using HotelService.Application.DTOs;
using HotelService.Application.Repositories;
using Shared.Models;

namespace HotelService.Application.Services;

public class HotelsService : IHotelsService
{
    private readonly IHotelRepository _hotelRepository;

    public HotelsService(IHotelRepository hotelRepository)
    {
        _hotelRepository = hotelRepository;
    }

    public Task<IEnumerable<Hotel>> GetAllHotelsAsync() =>
        _hotelRepository.GetAllAsync();

    public Task<Hotel?> GetHotelByIdAsync(int id) =>
        _hotelRepository.GetByIdAsync(id);

    public Task<Hotel> CreateHotelAsync(CreateHotelDto dto)
    {
        var hotel = new Hotel
        {
            Name = dto.Name,
            Address = dto.Address,
            City = dto.City,
            StarRating = dto.StarRating,
            Description = dto.Description
        };
        return _hotelRepository.CreateAsync(hotel);
    }

    public Task<Hotel?> UpdateHotelAsync(int id, UpdateHotelDto dto)
    {
        var hotel = new Hotel
        {
            Name = dto.Name,
            Address = dto.Address,
            City = dto.City,
            StarRating = dto.StarRating,
            Description = dto.Description
        };
        return _hotelRepository.UpdateAsync(id, hotel);
    }

    public Task<bool> DeleteHotelAsync(int id) =>
        _hotelRepository.DeleteAsync(id);
}
