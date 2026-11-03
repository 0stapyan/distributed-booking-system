using GuestScoreService.Models;
using GuestScoreService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuestScoreService.Controllers;

[ApiController]
[Authorize]
[Route("api/guest-score")]
public class GuestScoreController : ControllerBase
{
    private readonly IGuestScoreEvaluator _evaluator;
    private readonly ILogger<GuestScoreController> _logger;

    public GuestScoreController(IGuestScoreEvaluator evaluator, ILogger<GuestScoreController> logger)
    {
        _evaluator = evaluator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<GuestScoreResult>> Evaluate(GuestScoreRequest request, CancellationToken cancellationToken)
    {
        var result = await _evaluator.EvaluateAsync(request, cancellationToken);

        _logger.LogInformation(
            "Guest {GuestId} scored {Score} -> {Decision}",
            request.GuestId, result.Score, result.Decision);

        return result.Approved
            ? Ok(result)
            : StatusCode(StatusCodes.Status402PaymentRequired, result);
    }
}