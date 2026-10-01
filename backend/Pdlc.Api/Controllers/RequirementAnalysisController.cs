using Microsoft.AspNetCore.Mvc;
using Pdlc.Application.Requirements;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Api.Controllers;

/// <summary>Stage 1 — Requirement Analysis</summary>
[ApiController]
[Route("api/pdlc/requirements")]
public sealed class RequirementAnalysisController : ControllerBase
{
    private readonly IRequirementAnalysisService _service;
    private readonly IRequirementRepository _repo;

    public RequirementAnalysisController(
        IRequirementAnalysisService service,
        IRequirementRepository repo)
    {
        _service = service;
        _repo = repo;
    }

    /// <summary>
    /// Analyse raw free-text input and return a structured requirement document.
    /// Optionally creates an ADO Boards work item.
    /// </summary>
    [HttpPost("analyze")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Analyze(
        [FromBody] AnalyzeRequirementRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RawInput))
            return BadRequest("RawInput is required.");

        var doc = await _service.AnalyzeAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = doc.Id }, doc);
    }

    /// <summary>Continue a multi-turn refinement conversation on an existing requirement.</summary>
    [HttpPost("{id:guid}/refine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Refine(
        Guid id,
        [FromBody] RefineRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message is required.");

        try
        {
            var turn = await _service.RefineAsync(id, request.Message, ct);
            return Ok(turn);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>Sync a requirement to ADO Boards (creates User Story + child Tasks).</summary>
    [HttpPost("{id:guid}/sync-ado")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncToAdo(
        Guid id,
        [FromBody] SyncAdoRequest request,
        CancellationToken ct)
    {
        try
        {
            await _service.SyncToAdoAsync(id, request.AdoProject, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>Get a single requirement document by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var doc = await _repo.GetByIdAsync(id, ct);
        return doc is null ? NotFound() : Ok(doc);
    }

    /// <summary>List requirement documents (paginated).</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var docs = await _repo.ListAsync(page, pageSize, ct);
        return Ok(docs);
    }

    /// <summary>Get the full conversation history for a requirement.</summary>
    [HttpGet("{id:guid}/conversation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversation(Guid id, CancellationToken ct)
    {
        var turns = await _repo.GetConversationAsync(id, ct);
        return Ok(turns);
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record RefineRequest(string Message);
public record SyncAdoRequest(string AdoProject);
