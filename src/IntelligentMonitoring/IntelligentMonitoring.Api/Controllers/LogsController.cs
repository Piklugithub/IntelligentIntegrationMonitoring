
using IntelligentMonitoring.Application.Abstractions.Logging;
using IntelligentMonitoring.Application.Models.Logging;
using Microsoft.AspNetCore.Mvc;

namespace IntelligentMonitoring.Api.Controllers;

[ApiController]
[Route("api/logs")]
public sealed class LogsController(
    ILogSearchService logSearchService) : ControllerBase
{
    [HttpGet("transaction/{transactionId}")]
    [ProducesResponseType(typeof(IReadOnlyList<LokiLogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<LokiLogEntry>>>
        SearchByTransactionId(
            string transactionId,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            CancellationToken cancellationToken)
    {
        var endTime = to ?? DateTimeOffset.UtcNow;
        var startTime = from ?? endTime.AddHours(-24);

        if (startTime >= endTime)
        {
            return BadRequest("The 'from' time must be earlier than the 'to' time.");
        }

        var logs = await logSearchService.SearchByTransactionIdAsync(
            transactionId,
            startTime,
            endTime,
            cancellationToken);

        return Ok(logs);
    }

    [HttpGet("trace/{traceId}")]
    [ProducesResponseType(typeof(IReadOnlyList<LokiLogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<LokiLogEntry>>>
        SearchByTraceId(
            string traceId,
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            CancellationToken cancellationToken)
    {
        var endTime = to ?? DateTimeOffset.UtcNow;
        var startTime = from ?? endTime.AddHours(-24);

        if (startTime >= endTime)
        {
            return BadRequest("The 'from' time must be earlier than the 'to' time.");
        }

        var logs = await logSearchService.SearchByTraceIdAsync(
            traceId,
            startTime,
            endTime,
            cancellationToken);

        return Ok(logs);
    }
}
