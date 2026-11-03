using BookingService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Controllers;

[ApiController]
[Route("api/sagas")]
public class SagasController : ControllerBase
{
    private readonly BookingServiceDbContext _db;
    public SagasController(BookingServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _db.BookingSagas.OrderByDescending(s => s.CreatedAt).ToListAsync());
    [HttpGet("{correlationId:guid}")]
    public async Task<IActionResult> GetById(Guid correlationId)
    {
        var saga = await _db.BookingSagas.FindAsync(correlationId);
        return saga is null ? NotFound() : Ok(saga);
    }
}
