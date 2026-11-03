using GuestService.Application.Repositories;
using GuestService.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace GuestService.Repositories;

public class GuestRepository : IGuestRepository
{
    private readonly GuestDbContext _db;

    public GuestRepository(GuestDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Guest>> GetAllAsync() =>
        await _db.Guests.ToListAsync();

    public async Task<Guest?> GetByIdAsync(int id) =>
        await _db.Guests.FindAsync(id);

    public async Task<Guest?> GetByEmailAsync(string email) =>
        await _db.Guests.FirstOrDefaultAsync(g => g.Email == email);

    public async Task<Guest> CreateAsync(Guest guest)
    {
        _db.Guests.Add(guest);
        await _db.SaveChangesAsync();
        return guest;
    }

    public async Task<Guest?> UpdateAsync(int id, Guest guest)
    {
        var existing = await _db.Guests.FindAsync(id);
        if (existing is null) return null;

        existing.FirstName = guest.FirstName;
        existing.LastName = guest.LastName;
        existing.Email = guest.Email;
        existing.Phone = guest.Phone;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var guest = await _db.Guests.FindAsync(id);
        if (guest is null) return false;

        _db.Guests.Remove(guest);
        await _db.SaveChangesAsync();
        return true;
    }
}
