using HotelService.Application.DTOs;
using HotelService.Application.Services;
using HotelService.Observability;
using Microsoft.AspNetCore.Mvc;

namespace HotelService.Controllers;

[ApiController]
[Route("api/hotels")]
public class HotelsController : ControllerBase
{
    private readonly IHotelsService _hotelsService;
    private readonly HotelMetrics _metrics;
    private readonly ILogger<HotelsController> _logger;

    public HotelsController(IHotelsService hotelsService, HotelMetrics metrics, ILogger<HotelsController> logger)
    {
        _hotelsService = hotelsService;
        _metrics = metrics;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        _logger.LogInformation("Fetching all hotels");
        var hotels = await _hotelsService.GetAllHotelsAsync();
        return Ok(hotels);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        _logger.LogInformation("Fetching hotel {HotelId}", id);
        var hotel = await _hotelsService.GetHotelByIdAsync(id);
        if (hotel is null)
        {
            _logger.LogWarning("Hotel {HotelId} not found", id);
            return NotFound();
        }
        return Ok(hotel);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHotelDto dto)
    {
        _logger.LogInformation("Creating hotel {HotelName} in {City}", dto.Name, dto.City);
        var hotel = await _hotelsService.CreateHotelAsync(dto);
        _metrics.HotelsCreated.Add(1, new KeyValuePair<string, object?>("city", dto.City));
        _logger.LogInformation("Hotel created {HotelId} {HotelName}", hotel.Id, hotel.Name);
        return CreatedAtAction(nameof(GetById), new { id = hotel.Id }, hotel);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateHotelDto dto)
    {
        _logger.LogInformation("Updating hotel {HotelId}", id);
        var hotel = await _hotelsService.UpdateHotelAsync(id, dto);
        if (hotel is null)
        {
            _logger.LogWarning("Hotel {HotelId} not found for update", id);
            return NotFound();
        }
        return Ok(hotel);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        _logger.LogInformation("Deleting hotel {HotelId}", id);
        var deleted = await _hotelsService.DeleteHotelAsync(id);
        if (!deleted)
        {
            _logger.LogWarning("Hotel {HotelId} not found for deletion", id);
            return NotFound();
        }
        return NoContent();
    }
}