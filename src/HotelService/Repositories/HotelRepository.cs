using System.Diagnostics;
using System.Text.Json;
using HotelService.Application.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Models;

namespace HotelService.Repositories;

public class HotelRepository : IHotelRepository
{
    private readonly BookingDbContext _db;

    public HotelRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Hotel>> GetAllAsync() =>
        await _db.Hotels.Include(h => h.Rooms).ToListAsync();

    public async Task<Hotel?> GetByIdAsync(int id) =>
        await _db.Hotels.Include(h => h.Rooms).FirstOrDefaultAsync(h => h.Id == id);

    public async Task<Hotel> CreateAsync(Hotel hotel)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Hotels.Add(hotel);
        await _db.SaveChangesAsync();

        _db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = "hotel-created",
            Payload = JsonSerializer.Serialize(new
            {
                hotelId = hotel.Id,
                name = hotel.Name,
                city = hotel.City,
                starRating = hotel.StarRating,
            }),
            CreatedAt = DateTime.UtcNow,
            TraceParent = Activity.Current?.Id,
        });
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return hotel;
    }

    public async Task<Hotel?> UpdateAsync(int id, Hotel hotel)
    {
        var existing = await _db.Hotels.FindAsync(id);
        if (existing is null) return null;

        existing.Name = hotel.Name;
        existing.Address = hotel.Address;
        existing.City = hotel.City;
        existing.StarRating = hotel.StarRating;
        existing.Description = hotel.Description;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var hotel = await _db.Hotels.FindAsync(id);
        if (hotel is null) return false;

        _db.Hotels.Remove(hotel);
        await _db.SaveChangesAsync();
        return true;
    }
}
