using System.Diagnostics;
using System.Text.Json;
using BookingService.Application.Repositories;
using BookingService.Data;
using BookingService.Models;
using BookingService.Saga;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using Shared.Saga;

namespace BookingService.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly BookingServiceDbContext _db;
    private readonly SagaMetrics _metrics;

    public BookingRepository(BookingServiceDbContext db, SagaMetrics metrics)
    {
        _db = db;
        _metrics = metrics;
    }

    public async Task<IEnumerable<Booking>> GetAllAsync() =>
        await _db.Bookings.Include(b => b.Guests).ToListAsync();

    public async Task<IEnumerable<Booking>> GetAllByOwnerAsync(string ownerId) =>
        await _db.Bookings.Include(b => b.Guests).Where(b => b.OwnerId == ownerId).ToListAsync();

    public async Task<Booking?> GetByIdAsync(int id) =>
        await _db.Bookings.Include(b => b.Guests).FirstOrDefaultAsync(b => b.Id == id);

    // Created with AI
    public async Task<Guid> StartBookingSagaAsync(Booking booking)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        var saga = new BookingSaga
        {
            BookingId = booking.Id,
            State = SagaState.Started,
            RoomId = booking.RoomId,
            Amount = 0m,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            Guests = booking.Guests.Count,
        };
        _db.BookingSagas.Add(saga);

        var command = new ReserveRoomCommand(
            saga.CorrelationId, booking.Id, booking.RoomId,
            booking.CheckInDate, booking.CheckOutDate, booking.Guests.Count);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = SagaTopology.ReserveRoomKey,
            Exchange = SagaTopology.Exchange,
            Payload = JsonSerializer.Serialize(command),
            CorrelationId = saga.CorrelationId.ToString(),
            CreatedAt = DateTime.UtcNow,
            TraceParent = Activity.Current?.Id,
        });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        _metrics.RecordStarted();
        _metrics.IncrementActive();

        return saga.CorrelationId;
    }

    public async Task<Booking?> UpdateStatusAsync(int id, BookingStatus status)
    {
        var booking = await _db.Bookings.FindAsync(id);
        if (booking is null) return null;

        booking.Status = status;
        await _db.SaveChangesAsync();
        return booking;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var booking = await _db.Bookings.FindAsync(id);
        if (booking is null) return false;

        _db.Bookings.Remove(booking);
        await _db.SaveChangesAsync();
        return true;
    }
}
