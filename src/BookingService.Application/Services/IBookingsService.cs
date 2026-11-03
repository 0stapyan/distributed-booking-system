using BookingService.Application.Clients;
using BookingService.Application.DTOs;
using Shared.Models;

namespace BookingService.Application.Services;

public interface IBookingsService
{
    Task<IEnumerable<Booking>> GetBookingsByOwnerAsync(string ownerId);
    Task<Booking?> GetBookingByIdAsync(int id);
    Task<(Booking? booking, Guid? correlationId, string? error, GuestScoreDecision? declined)> CreateBookingAsync(CreateBookingDto dto, string ownerId);
    Task<Booking?> UpdateBookingStatusAsync(int id, BookingStatus status);
    Task<bool> DeleteBookingAsync(int id);
}
