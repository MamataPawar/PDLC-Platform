using Microsoft.AspNetCore.Mvc;
using Pdlc.Application.CodeGen;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Api.Controllers;

/// <summary>Stage 3a — Code Generation</summary>
[ApiController]
[Route("api/pdlc/codegen")]
public sealed class CodeGenerationController : ControllerBase
{
    private readonly ICodeGenerationService _service;
    private readonly ICodeGenRepository _repo;

    public CodeGenerationController(ICodeGenerationService service, ICodeGenRepository repo)
    {
        _service = service;
        _repo = repo;
    }

    /// <summary>Generate boilerplate .NET Core and/or Angular code from a requirement.</summary>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Generate([FromBody] CodeGenRequest request, CancellationToken ct)
    {
        try
        {
            var results = await _service.GenerateAsync(request, ct);
            return StatusCode(201, results);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Commit previously generated code to an ADO Repo branch.</summary>
    [HttpPost("{requirementId:guid}/commit-ado")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CommitToAdo(
        Guid requirementId,
        [FromBody] CommitAdoRequest request,
        CancellationToken ct)
    {
        await _service.CommitToAdoAsync(requirementId, request.AdoProject, request.Repository, request.Branch, ct);
        return NoContent();
    }

    /// <summary>List all generated code artifacts for a requirement.</summary>
    [HttpGet("{requirementId:guid}")]
    public async Task<IActionResult> GetByRequirement(Guid requirementId, CancellationToken ct)
        => Ok(await _repo.GetByRequirementIdAsync(requirementId, ct));
}

public record CommitAdoRequest(string AdoProject, string Repository, string Branch);
