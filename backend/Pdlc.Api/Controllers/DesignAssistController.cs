using Microsoft.AspNetCore.Mvc;
using Pdlc.Application.Design;
using Pdlc.Domain.Enums;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Api.Controllers;

/// <summary>Stage 2 — Design Assist</summary>
[ApiController]
[Route("api/pdlc/design")]
public sealed class DesignAssistController : ControllerBase
{
    private readonly IDesignAssistService _service;
    private readonly IDesignArtifactRepository _repo;

    public DesignAssistController(IDesignAssistService service, IDesignArtifactRepository repo)
    {
        _service = service;
        _repo = repo;
    }

    /// <summary>Generate all design artifacts for a requirement document.</summary>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Generate([FromBody] GenerateDesignRequest request, CancellationToken ct)
    {
        try
        {
            var artifacts = await _service.GenerateAsync(request, ct);
            return CreatedAtAction(nameof(GetByRequirement),
                new { requirementId = request.RequirementDocumentId }, artifacts);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Regenerate a single artifact type with optional additional context.</summary>
    [HttpPost("{requirementId:guid}/regenerate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Regenerate(
        Guid requirementId,
        [FromBody] RegenerateRequest request,
        CancellationToken ct)
    {
        try
        {
            var artifact = await _service.RegenerateArtifactAsync(requirementId, request.Type, request.AdditionalContext, ct);
            return Ok(artifact);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Get all design artifacts for a requirement.</summary>
    [HttpGet("{requirementId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByRequirement(Guid requirementId, CancellationToken ct)
    {
        var artifacts = await _repo.GetByRequirementIdAsync(requirementId, ct);
        return Ok(artifacts);
    }
}

public record RegenerateRequest(DesignArtifactType Type, string? AdditionalContext = null);
