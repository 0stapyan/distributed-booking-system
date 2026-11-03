using System.Diagnostics;
using GuestService.Application.DTOs;
using GuestService.Application.Metrics;
using GuestService.Application.Repositories;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace GuestService.Application.Services;

public class GuestsService : IGuestsService
{
    private readonly IGuestRepository _guestRepository;
    private readonly ILogger<GuestsService> _logger;
    private readonly IGuestMetrics _metrics;

    public GuestsService(
        IGuestRepository guestRepository,
        ILogger<GuestsService> logger,
        IGuestMetrics metrics)
    {
        _guestRepository = guestRepository;
        _logger = logger;
        _metrics = metrics;
    }

    public Task<IEnumerable<Guest>> GetAllGuestsAsync() =>
        _guestRepository.GetAllAsync();

    public async Task<Guest?> GetGuestByIdAsync(int id)
    {
        var guest = await _guestRepository.GetByIdAsync(id);
        if (guest is null)
            _logger.LogWarning("Guest {GuestId} not found", id);
        return guest;
    }

    public async Task<Guest?> GetGuestByEmailAsync(string email)
    {
        var guest = await _guestRepository.GetByEmailAsync(email);
        if (guest is null)
            _logger.LogWarning("Guest with email {Email} not found", email);
        return guest;
    }

    public async Task<Guest> CreateGuestAsync(CreateGuestDto dto)
    {
        var sw = Stopwatch.StartNew();

        var guest = new Guest
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone,
            CreatedAt = DateTime.UtcNow
        };
        var created = await _guestRepository.CreateAsync(guest);

        sw.Stop();
        _metrics.GuestCreated();
        _metrics.RecordCreationDuration(sw.Elapsed.TotalMilliseconds);

        _logger.LogInformation("Guest created: {GuestId}, name {FirstName} {LastName}, email {Email}",
            created.Id, created.FirstName, created.LastName, created.Email);
        return created;
    }

    public async Task<Guest?> UpdateGuestAsync(int id, UpdateGuestDto dto)
    {
        var guest = new Guest
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Phone = dto.Phone
        };
        var updated = await _guestRepository.UpdateAsync(id, guest);
        if (updated is null)
            _logger.LogWarning("Update failed: guest {GuestId} not found", id);
        else
            _logger.LogInformation("Guest {GuestId} updated, new email {Email}", id, updated.Email);
        return updated;
    }

    public async Task<bool> DeleteGuestAsync(int id)
    {
        var deleted = await _guestRepository.DeleteAsync(id);
        if (deleted)
            _logger.LogInformation("Guest {GuestId} deleted", id);
        else
            _logger.LogWarning("Delete failed: guest {GuestId} not found", id);
        return deleted;
    }
}