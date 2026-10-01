using Microsoft.AspNetCore.Mvc;
using Pdlc.Application.TestGen;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Api.Controllers;

/// <summary>Stage 4 — AI Test Generation</summary>
[ApiController]
[Route("api/pdlc/testgen")]
public sealed class TestGenerationController : ControllerBase
{
    private readonly ITestGenerationService _service;
    private readonly ITestGenRepository _repo;

    public TestGenerationController(ITestGenerationService service, ITestGenRepository repo)
    {
        _service = service;
        _repo = repo;
    }

    /// <summary>
    /// Generate xUnit and/or Jasmine test stubs from source code.
    /// Called on-demand or by the azure-pipelines.yml ai-test-generation stage.
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Generate([FromBody] TestGenRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SourceContent))
            return BadRequest("SourceContent is required.");

        var results = await _service.GenerateAsync(request, ct);
        return StatusCode(201, results);
    }

    /// <summary>Commit generated test files to ADO Repo (used by pipeline stage).</summary>
    [HttpPost("{requirementId:guid}/commit-ado")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CommitToAdo(
        Guid requirementId, [FromBody] TestCommitRequest request, CancellationToken ct)
    {
        await _service.CommitToAdoAsync(requirementId, request.AdoProject, request.Repository, request.Branch, ct);
        return NoContent();
    }

    /// <summary>Download a single generated test file as plain text.</summary>
    [HttpGet("{requirementId:guid}")]
    public async Task<IActionResult> GetByRequirement(Guid requirementId, CancellationToken ct)
        => Ok(await _repo.GetByRequirementIdAsync(requirementId, ct));
}

public record TestCommitRequest(string AdoProject, string Repository, string Branch);
