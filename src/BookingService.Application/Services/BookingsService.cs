using BookingService.Application.Clients;
using BookingService.Application.DTOs;
using BookingService.Application.ReadModels;
using BookingService.Application.Repositories;
using BookingService.Application.Telemetry;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace BookingService.Application.Services;

public class BookingsService : IBookingsService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IMasterDataReader _masterData;
    private readonly IGuestScoreClient _guestScore;
    private readonly ILogger<BookingsService> _logger;
    private readonly BookingMetrics _metrics;

    public BookingsService(
        IBookingRepository bookingRepository,
        IMasterDataReader masterData,
        IGuestScoreClient guestScore,
        ILogger<BookingsService> logger,
        BookingMetrics metrics)
    {
        _bookingRepository = bookingRepository;
        _masterData = masterData;
        _guestScore = guestScore;
        _logger = logger;
        _metrics = metrics;
    }

    public Task<IEnumerable<Booking>> GetBookingsByOwnerAsync(string ownerId) =>
        _bookingRepository.GetAllByOwnerAsync(ownerId);

    public Task<Booking?> GetBookingByIdAsync(int id) =>
        _bookingRepository.GetByIdAsync(id);

    public async Task<(Booking? booking, Guid? correlationId, string? error, GuestScoreDecision? declined)> CreateBookingAsync(CreateBookingDto dto, string ownerId)
    {
        if (dto.GuestIds is null || dto.GuestIds.Count == 0)
            return (null, null, "Необхідно вказати хоча б одного гостя", null);

        var nights = (dto.CheckOutDate - dto.CheckInDate).Days;
        if (nights <= 0)
            return (null, null, "Дата виїзду має бути пізніше дати заїзду", null);

        var room = await _masterData.GetRoomAsync(dto.RoomId, CancellationToken.None);
        if (room is null)
            return (null, null, $"Кімната {dto.RoomId} не знайдена", null);
        if (!room.IsAvailable)
            return (null, null, $"Кімната {dto.RoomId} недоступна", null);
        if (dto.GuestIds.Count > room.MaxGuests)
            return (null, null, $"Кімната розрахована максимум на {room.MaxGuests} гостей", null);

        if (!await _masterData.AllGuestsExistAsync(dto.GuestIds, CancellationToken.None))
            return (null, null, "Одного з гостей не знайдено", null);

        var primaryGuestId = dto.GuestIds[0];
        var decision = await _guestScore.EvaluateAsync(primaryGuestId, dto.RoomId, 0m, CancellationToken.None);
        if (!decision.Approved)
        {
            _logger.LogInformation("[guest-score] booking declined for guest {GuestId}: score {Score} ({Decision})",
                primaryGuestId, decision.Score, decision.Decision);
            return (null, null, null, decision);
        }

        var booking = new Booking
        {
            RoomId = dto.RoomId,
            OwnerId = ownerId,
            CheckInDate = dto.CheckInDate,
            CheckOutDate = dto.CheckOutDate,
            TotalPrice = 0m,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Guests = dto.GuestIds.Select(gid => new BookingGuest { GuestId = gid }).ToList()
        };

        var correlationId = await _bookingRepository.StartBookingSagaAsync(booking);
        _metrics.RecordBookingCreated(dto.RoomId);
        _logger.LogInformation("Saga {CorrelationId} started for booking {BookingId}, room {RoomId}, guests {GuestCount}", correlationId, booking.Id, dto.RoomId, dto.GuestIds.Count);
        return (booking, correlationId, null, null);
    }

    public Task<Booking?> UpdateBookingStatusAsync(int id, BookingStatus status) =>
        _bookingRepository.UpdateStatusAsync(id, status);

    public Task<bool> DeleteBookingAsync(int id) =>
        _bookingRepository.DeleteAsync(id);
}
