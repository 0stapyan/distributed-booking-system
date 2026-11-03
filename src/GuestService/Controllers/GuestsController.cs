using GuestService.Application.DTOs;
using GuestService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace GuestService.Controllers;

[ApiController]
[Route("api/guests")]
public class GuestsController : ControllerBase
{
    private readonly IGuestsService _guestsService;

    public GuestsController(IGuestsService guestsService)
    {
        _guestsService = guestsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _guestsService.GetAllGuestsAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var guest = await _guestsService.GetGuestByIdAsync(id);
        return guest is null ? NotFound() : Ok(guest);
    }

    [HttpGet("by-email")]
    public async Task<IActionResult> GetByEmail([FromQuery] string email)
    {
        var guest = await _guestsService.GetGuestByEmailAsync(email);
        return guest is null ? NotFound() : Ok(guest);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGuestDto dto)
    {
        var guest = await _guestsService.CreateGuestAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = guest.Id }, guest);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGuestDto dto)
    {
        var guest = await _guestsService.UpdateGuestAsync(id, dto);
        return guest is null ? NotFound() : Ok(guest);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _guestsService.DeleteGuestAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
