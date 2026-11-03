using System.Security.Claims;
using BookingService.Application.DTOs;
using BookingService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingService.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingsService _bookingsService;

    public BookingsController(IBookingsService bookingsService)
    {
        _bookingsService = bookingsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var ownerId = GetOwnerId();
        if (ownerId is null) return Unauthorized();
        return Ok(await _bookingsService.GetBookingsByOwnerAsync(ownerId));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var ownerId = GetOwnerId();
        if (ownerId is null) return Unauthorized();

        var booking = await _bookingsService.GetBookingByIdAsync(id);
        if (booking is null) return NotFound();
        if (booking.OwnerId != ownerId) return Forbid();
        return Ok(booking);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBookingDto dto)
    {
        var ownerId = GetOwnerId();
        if (ownerId is null) return Unauthorized();

        var (booking, correlationId, error, declined) = await _bookingsService.CreateBookingAsync(dto, ownerId);
        if (declined is not null)
            return StatusCode(StatusCodes.Status402PaymentRequired,
                new { error = "Prepayment required", score = declined.Score, decision = declined.Decision, factors = declined.Factors });
        if (error is not null)
            return BadRequest(new { error });

        return Accepted(new { bookingId = booking!.Id, correlationId, status = booking.Status.ToString() });
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusDto dto)
    {
        var ownerId = GetOwnerId();
        if (ownerId is null) return Unauthorized();

        var existing = await _bookingsService.GetBookingByIdAsync(id);
        if (existing is null) return NotFound();
        if (existing.OwnerId != ownerId) return Forbid();

        var booking = await _bookingsService.UpdateBookingStatusAsync(id, dto.Status);
        return booking is null ? NotFound() : Ok(booking);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ownerId = GetOwnerId();
        if (ownerId is null) return Unauthorized();

        var existing = await _bookingsService.GetBookingByIdAsync(id);
        if (existing is null) return NotFound();
        if (existing.OwnerId != ownerId) return Forbid();

        var deleted = await _bookingsService.DeleteBookingAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    private string? GetOwnerId()
    {
        return User.FindFirstValue("sub")
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("preferred_username")
               ?? User.FindFirstValue(ClaimTypes.Email);
    }
}
