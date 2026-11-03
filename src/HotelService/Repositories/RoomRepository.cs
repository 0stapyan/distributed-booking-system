using HotelService.Application.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Data;
using Shared.Models;

namespace HotelService.Repositories;

public class RoomRepository : IRoomRepository
{
    private readonly BookingDbContext _db;

    public RoomRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Room>> GetAllAsync() =>
        await _db.Rooms.ToListAsync();

    public async Task<IEnumerable<Room>> GetByHotelIdAsync(int hotelId) =>
        await _db.Rooms.Where(r => r.HotelId == hotelId).ToListAsync();

    public async Task<Room?> GetByIdAsync(int id) =>
        await _db.Rooms.FindAsync(id);

    public async Task<Room> CreateAsync(Room room)
    {
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        return room;
    }

    public async Task<Room?> UpdateAsync(int id, Room room)
    {
        var existing = await _db.Rooms.FindAsync(id);
        if (existing is null) return null;

        existing.RoomNumber = room.RoomNumber;
        existing.Type = room.Type;
        existing.PricePerNight = room.PricePerNight;
        existing.MaxGuests = room.MaxGuests;
        existing.IsAvailable = room.IsAvailable;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room is null) return false;

        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetAvailabilityAsync(int id, bool isAvailable)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room is null) return false;

        room.IsAvailable = isAvailable;
        await _db.SaveChangesAsync();
        return true;
    }
}
