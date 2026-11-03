using HotelService.Application.DTOs;
using HotelService.Application.Services;
using HotelService.Observability;
using Microsoft.AspNetCore.Mvc;

namespace HotelService.Controllers;

[ApiController]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomsService _roomsService;
    private readonly HotelMetrics _metrics;
    private readonly ILogger<RoomsController> _logger;

    public RoomsController(IRoomsService roomsService, HotelMetrics metrics, ILogger<RoomsController> logger)
    {
        _roomsService = roomsService;
        _metrics = metrics;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? hotelId)
    {
        _logger.LogInformation("Fetching rooms {HotelId}", hotelId);
        if (hotelId.HasValue)
            return Ok(await _roomsService.GetRoomsByHotelIdAsync(hotelId.Value));
        return Ok(await _roomsService.GetAllRoomsAsync());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        _logger.LogInformation("Fetching room {RoomId}", id);
        var room = await _roomsService.GetRoomByIdAsync(id);
        if (room is null)
        {
            _logger.LogWarning("Room {RoomId} not found", id);
            return NotFound();
        }
        return Ok(room);
    }

    [HttpGet("{id}/availability")]
    public async Task<IActionResult> CheckAvailability(int id, [FromQuery] DateTime checkIn, [FromQuery] DateTime checkOut)
    {
        _logger.LogInformation("Checking availability for room {RoomId} from {CheckIn} to {CheckOut}", id, checkIn, checkOut);
        var available = await _roomsService.CheckAvailabilityAsync(id, checkIn, checkOut);
        return Ok(new { roomId = id, isAvailable = available });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoomDto dto)
    {
        _logger.LogInformation("Creating room {RoomNumber} for hotel {HotelId} at price {PricePerNight}", dto.RoomNumber, dto.HotelId, dto.PricePerNight);
        var room = await _roomsService.CreateRoomAsync(dto);
        _metrics.RoomsCreated.Add(1, new KeyValuePair<string, object?>("room_type", dto.Type.ToString()));
        _metrics.RoomPricePerNight.Record((double)dto.PricePerNight, new KeyValuePair<string, object?>("room_type", dto.Type.ToString()));
        _logger.LogInformation("Room created {RoomId} {RoomNumber} PricePerNight={PricePerNight}", room.Id, room.RoomNumber, room.PricePerNight);
        return CreatedAtAction(nameof(GetById), new { id = room.Id }, room);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRoomDto dto)
    {
        _logger.LogInformation("Updating room {RoomId}", id);
        var room = await _roomsService.UpdateRoomAsync(id, dto);
        if (room is null)
        {
            _logger.LogWarning("Room {RoomId} not found for update", id);
            return NotFound();
        }
        return Ok(room);
    }

    [HttpPatch("{id}/availability")]
    public async Task<IActionResult> SetAvailability(int id, [FromBody] SetAvailabilityDto dto)
    {
        _logger.LogInformation("Setting availability for room {RoomId} to {IsAvailable}", id, dto.IsAvailable);
        var updated = await _roomsService.SetAvailabilityAsync(id, dto.IsAvailable);
        if (!updated)
        {
            _logger.LogWarning("Room {RoomId} not found for availability update", id);
            return NotFound();
        }
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        _logger.LogInformation("Deleting room {RoomId}", id);
        var deleted = await _roomsService.DeleteRoomAsync(id);
        if (!deleted)
        {
            _logger.LogWarning("Room {RoomId} not found for deletion", id);
            return NotFound();
        }
        return NoContent();
    }
}

public record SetAvailabilityDto(bool IsAvailable);