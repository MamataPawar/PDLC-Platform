using Microsoft.AspNetCore.Mvc;
using Pdlc.Application.PrReview;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Api.Controllers;

/// <summary>Stage 3b — AI-Powered PR Review</summary>
[ApiController]
[Route("api/pdlc/pr-review")]
public sealed class PrReviewController : ControllerBase
{
    private readonly IPrReviewService _service;
    private readonly IPrReviewRepository _repo;

    public PrReviewController(IPrReviewService service, IPrReviewRepository repo)
    {
        _service = service;
        _repo = repo;
    }

    /// <summary>
    /// Trigger an AI review of an ADO pull request.
    /// Fetches diff, calls Claude, posts inline comments to the PR.
    /// This endpoint is called by the azure-pipelines.yml ai-code-review stage.
    /// </summary>
    [HttpPost("review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Review([FromBody] ReviewPrRequest request, CancellationToken ct)
    {
        var result = await _service.ReviewPullRequestAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Get the latest review result for a specific PR.</summary>
    [HttpGet("{adoProject}/{repository}/{pullRequestId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReview(
        string adoProject, string repository, int pullRequestId, CancellationToken ct)
    {
        var result = await _repo.GetByPrIdAsync(adoProject, repository, pullRequestId, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
