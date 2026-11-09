
using IntelligentMonitoring.Application.Abstractions.Investigations;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentMonitoring.Api.Controllers;

[ApiController]
[Route("api/investigations")]
public sealed class InvestigationsController(
    IFailureInvestigationService investigationService)
    : ControllerBase
{
    [HttpGet("transactions/{transactionId}")]
    public async Task<IActionResult> InvestigateTransaction(
        string transactionId,
        [FromQuery] DateTimeOffset startTime,
        [FromQuery] DateTimeOffset endTime,
        CancellationToken cancellationToken)
    {
        if (startTime >= endTime)
        {
            return BadRequest(
                "Start time must be earlier than end time.");
        }

        var result =
            await investigationService.InvestigateTransactionAsync(
                transactionId,
                startTime,
                endTime,
                cancellationToken);

        return Ok(result);
    }
}
