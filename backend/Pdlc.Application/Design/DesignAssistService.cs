using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pdlc.Domain.Entities;
using Pdlc.Domain.Enums;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Application.Design;

public interface IDesignAssistService
{
    /// <summary>Generate all design artifacts (component tree, API contract, data model) for a requirement.</summary>
    Task<IReadOnlyList<DesignArtifact>> GenerateAsync(GenerateDesignRequest request, CancellationToken ct = default);

    /// <summary>Regenerate a single artifact type.</summary>
    Task<DesignArtifact> RegenerateArtifactAsync(Guid requirementId, DesignArtifactType type, string? additionalContext, CancellationToken ct = default);
}

public record GenerateDesignRequest(
    Guid RequirementDocumentId,
    string CreatedBy = "system",
    string PromptVersion = "v1"
);

public sealed class DesignAssistService : IDesignAssistService
{
    private readonly IClaudeService _claude;
    private readonly IPromptRepository _prompts;
    private readonly IRequirementRepository _requirementRepo;
    private readonly IDesignArtifactRepository _designRepo;
    private readonly ILogger<DesignAssistService> _logger;

    public DesignAssistService(
        IClaudeService claude,
        IPromptRepository prompts,
        IRequirementRepository requirementRepo,
        IDesignArtifactRepository designRepo,
        ILogger<DesignAssistService> logger)
    {
        _claude = claude;
        _prompts = prompts;
        _requirementRepo = requirementRepo;
        _designRepo = designRepo;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DesignArtifact>> GenerateAsync(
        GenerateDesignRequest request,
        CancellationToken ct = default)
    {
        var doc = await _requirementRepo.GetByIdAsync(request.RequirementDocumentId, ct)
            ?? throw new KeyNotFoundException($"RequirementDocument {request.RequirementDocumentId} not found");

        var systemPrompt = await _prompts.GetPromptAsync(PdlcStage.Design, request.PromptVersion, ct);

        var userMessage = $"""
            Generate design artifacts for the following requirement:

            Title: {doc.Title}
            Summary: {doc.Summary}

            Acceptance Criteria:
            {string.Join("\n", doc.AcceptanceCriteria.Select((ac, i) => $"{i + 1}. {ac.Description}"))}

            Dependencies:
            {string.Join("\n", doc.Dependencies.Select(d => $"- {d.Name} ({d.Type})"))}

            Return a JSON object with three keys: componentTree, apiContract, dataModel.
            """;

        var claudeRequest = new ClaudeRequest(
            SystemPrompt: systemPrompt,
            UserMessage: userMessage,
            Stage: PdlcStage.Design,
            OperationName: "DesignGeneration"
        );

        var response = await _claude.SendAsync(claudeRequest, ct);
        var parsed = ParseDesignOutput(response.Content);

        var artifacts = new List<DesignArtifact>
        {
            await _designRepo.AddAsync(new DesignArtifact
            {
                RequirementDocumentId = doc.Id,
                Type = DesignArtifactType.ComponentTree,
                Title = $"Component Tree — {doc.Title}",
                Content = JsonSerializer.Serialize(parsed.ComponentTree),
                Format = "json",
                TokensUsed = response.InputTokens + response.OutputTokens,
                ModelVersion = response.Model,
                CreatedBy = request.CreatedBy
            }, ct),
            await _designRepo.AddAsync(new DesignArtifact
            {
                RequirementDocumentId = doc.Id,
                Type = DesignArtifactType.ApiContract,
                Title = $"API Contract — {doc.Title}",
                Content = parsed.ApiContract,
                Format = "yaml",
                TokensUsed = 0, // tokens already counted above
                ModelVersion = response.Model,
                CreatedBy = request.CreatedBy
            }, ct),
            await _designRepo.AddAsync(new DesignArtifact
            {
                RequirementDocumentId = doc.Id,
                Type = DesignArtifactType.DataModel,
                Title = $"Data Model — {doc.Title}",
                Content = parsed.DataModel,
                Format = "markdown",
                TokensUsed = 0,
                ModelVersion = response.Model,
                CreatedBy = request.CreatedBy
            }, ct)
        };

        _logger.LogInformation("[Stage2] Generated {Count} design artifacts for requirement {Id}", artifacts.Count, doc.Id);
        return artifacts;
    }

    public async Task<DesignArtifact> RegenerateArtifactAsync(
        Guid requirementId,
        DesignArtifactType type,
        string? additionalContext,
        CancellationToken ct = default)
    {
        var doc = await _requirementRepo.GetByIdAsync(requirementId, ct)
            ?? throw new KeyNotFoundException($"RequirementDocument {requirementId} not found");

        var systemPrompt = await _prompts.GetPromptAsync(PdlcStage.Design, ct: ct);
        var typeLabel = type.ToString();

        var userMessage = $"""
            Regenerate only the {typeLabel} design artifact for:
            Title: {doc.Title}
            Summary: {doc.Summary}
            {(additionalContext is not null ? $"\nAdditional context: {additionalContext}" : string.Empty)}
            """;

        var response = await _claude.SendAsync(new ClaudeRequest(
            SystemPrompt: systemPrompt,
            UserMessage: userMessage,
            Stage: PdlcStage.Design,
            OperationName: $"Regenerate{typeLabel}"), ct);

        return await _designRepo.AddAsync(new DesignArtifact
        {
            RequirementDocumentId = requirementId,
            Type = type,
            Title = $"{typeLabel} (Regenerated) — {doc.Title}",
            Content = response.Content,
            Format = type == DesignArtifactType.ApiContract ? "yaml" : "json",
            TokensUsed = response.InputTokens + response.OutputTokens,
            ModelVersion = response.Model
        }, ct);
    }

    private static DesignOutput ParseDesignOutput(string content)
    {
        var clean = content.Replace("```json", string.Empty).Replace("```yaml", string.Empty)
                           .Replace("```", string.Empty).Trim();
        try
        {
            return JsonSerializer.Deserialize<DesignOutput>(clean,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DesignOutput();
        }
        catch
        {
            return new DesignOutput { ApiContract = content };
        }
    }

    private sealed class DesignOutput
    {
        public object? ComponentTree { get; set; }
        public string ApiContract { get; set; } = string.Empty;
        public string DataModel { get; set; } = string.Empty;
    }
}
