using Microsoft.AspNetCore.Mvc;
using Pdlc.Domain.Enums;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Api.Controllers;

/// <summary>Token usage and cost observability across all PDLC stages.</summary>
[ApiController]
[Route("api/pdlc/usage")]
public sealed class UsageController : ControllerBase
{
    private readonly IUsageLogRepository _repo;
    public UsageController(IUsageLogRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] PdlcStage? stage,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var fromDate = from ?? DateTimeOffset.UtcNow.AddDays(-30);
        var toDate = to ?? DateTimeOffset.UtcNow;

        if (stage.HasValue)
        {
            var logs = await _repo.GetByStageAsync(stage.Value, fromDate, toDate, ct);
            return Ok(new
            {
                Stage = stage.Value.ToString(),
                From = fromDate,
                To = toDate,
                TotalCalls = logs.Count,
                TotalTokens = logs.Sum(l => l.TotalTokens),
                TotalInputTokens = logs.Sum(l => l.InputTokens),
                TotalOutputTokens = logs.Sum(l => l.OutputTokens),
                AvgDurationMs = logs.Any() ? logs.Average(l => l.DurationMs) : 0,
                Logs = logs
            });
        }

        // All stages
        var allLogs = new List<object>();
        foreach (var s in Enum.GetValues<PdlcStage>())
        {
            var stageLogs = await _repo.GetByStageAsync(s, fromDate, toDate, ct);
            allLogs.Add(new
            {
                Stage = s.ToString(),
                TotalCalls = stageLogs.Count,
                TotalTokens = stageLogs.Sum(l => l.TotalTokens),
                AvgDurationMs = stageLogs.Any() ? stageLogs.Average(l => l.DurationMs) : 0
            });
        }
        return Ok(allLogs);
    }
}
