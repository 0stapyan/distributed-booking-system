using Shared.Models;

namespace BookingService.Application.Repositories;

public interface IBookingRepository
{
    Task<IEnumerable<Booking>> GetAllAsync();
    Task<IEnumerable<Booking>> GetAllByOwnerAsync(string ownerId);
    Task<Booking?> GetByIdAsync(int id);
    Task<Guid> StartBookingSagaAsync(Booking booking);
    Task<Booking?> UpdateStatusAsync(int id, BookingStatus status);
    Task<bool> DeleteAsync(int id);
}
