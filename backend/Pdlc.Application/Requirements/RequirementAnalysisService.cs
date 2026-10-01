using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pdlc.Domain.Entities;
using Pdlc.Domain.Enums;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Application.Requirements;

public interface IRequirementAnalysisService
{
    /// <summary>Analyse raw input text → structured RequirementDocument.</summary>
    Task<RequirementDocument> AnalyzeAsync(AnalyzeRequirementRequest request, CancellationToken ct = default);

    /// <summary>Continue a multi-turn refinement conversation on an existing document.</summary>
    Task<ConversationTurn> RefineAsync(Guid requirementId, string userMessage, CancellationToken ct = default);

    /// <summary>Sync an existing RequirementDocument to ADO Boards as a User Story + child Tasks.</summary>
    Task SyncToAdoAsync(Guid requirementId, string adoProject, CancellationToken ct = default);
}

public record AnalyzeRequirementRequest(
    string RawInput,
    string CreatedBy = "system",
    string? AdoProject = null,
    bool SyncToAdo = false,
    string PromptVersion = "v1"
);

public sealed class RequirementAnalysisService : IRequirementAnalysisService
{
    private readonly IClaudeService _claude;
    private readonly IPromptRepository _prompts;
    private readonly IRequirementRepository _repo;
    private readonly IAdoBoardsService _ado;
    private readonly ILogger<RequirementAnalysisService> _logger;

    public RequirementAnalysisService(
        IClaudeService claude,
        IPromptRepository prompts,
        IRequirementRepository repo,
        IAdoBoardsService ado,
        ILogger<RequirementAnalysisService> logger)
    {
        _claude = claude;
        _prompts = prompts;
        _repo = repo;
        _ado = ado;
        _logger = logger;
    }

    public async Task<RequirementDocument> AnalyzeAsync(
        AnalyzeRequirementRequest request,
        CancellationToken ct = default)
    {
        var systemPrompt = await _prompts.GetPromptAsync(PdlcStage.Requirements, request.PromptVersion, ct);

        var claudeRequest = new ClaudeRequest(
            SystemPrompt: systemPrompt,
            UserMessage: $"Analyse the following requirement:\n\n{request.RawInput}",
            Stage: PdlcStage.Requirements,
            OperationName: "RequirementAnalysis"
        );

        var response = await _claude.SendAsync(claudeRequest, ct);

        // Parse structured JSON from Claude response
        var structured = ParseStructuredRequirement(response.Content);

        // Persist
        var doc = new RequirementDocument
        {
            RawInput = request.RawInput,
            Title = structured.Title,
            Summary = structured.Summary,
            Status = RequirementStatus.Analyzed,
            CreatedBy = request.CreatedBy,
            TokensUsed = response.InputTokens + response.OutputTokens,
            ModelVersion = response.Model,
            AcceptanceCriteria = structured.AcceptanceCriteria
                .Select((ac, i) => new AcceptanceCriterion { Description = ac, OrderIndex = i }).ToList(),
            EdgeCases = structured.EdgeCases
                .Select(ec => new EdgeCase { Description = ec.Description, Severity = ec.Severity }).ToList(),
            Dependencies = structured.Dependencies
                .Select(d => new Dependency { Name = d.Name, Type = d.Type, Notes = d.Notes }).ToList(),
            RiskFlags = structured.Risks
                .Select(r => new RiskFlag { Description = r.Description, Severity = r.Severity, Mitigation = r.Mitigation }).ToList(),
            EffortEstimate = new EffortEstimate
            {
                StoryPoints = structured.Effort.StoryPoints,
                Confidence = structured.Effort.Confidence,
                EstimatedDaysLow = structured.Effort.DaysLow,
                EstimatedDaysHigh = structured.Effort.DaysHigh,
                Rationale = structured.Effort.Rationale
            },
            ConversationHistory = new List<ConversationTurn>
            {
                new() { Role = "user",      Content = request.RawInput,    TurnIndex = 0 },
                new() { Role = "assistant", Content = response.Content,     TurnIndex = 1, TokensUsed = response.OutputTokens }
            }
        };

        await _repo.AddAsync(doc, ct);
        _logger.LogInformation("[Stage1] Created RequirementDocument {Id} — {Points} SP", doc.Id, doc.EffortEstimate.StoryPoints);

        // Optionally sync to ADO Boards
        if (request.SyncToAdo && !string.IsNullOrEmpty(request.AdoProject))
            await SyncToAdoAsync(doc.Id, request.AdoProject, ct);

        return doc;
    }

    public async Task<ConversationTurn> RefineAsync(
        Guid requirementId,
        string userMessage,
        CancellationToken ct = default)
    {
        var doc = await _repo.GetByIdAsync(requirementId, ct)
            ?? throw new KeyNotFoundException($"RequirementDocument {requirementId} not found");

        var systemPrompt = await _prompts.GetPromptAsync(PdlcStage.Requirements, ct: ct);
        var history = await _repo.GetConversationAsync(requirementId, ct);

        var response = await _claude.ContinueConversationAsync(history, userMessage, systemPrompt, ct);

        var nextIndex = history.Count > 0 ? history.Max(t => t.TurnIndex) + 1 : 0;

        var userTurn = new ConversationTurn
        {
            RequirementDocumentId = requirementId,
            Role = "user",
            Content = userMessage,
            TurnIndex = nextIndex
        };
        var assistantTurn = new ConversationTurn
        {
            RequirementDocumentId = requirementId,
            Role = "assistant",
            Content = response.Content,
            TurnIndex = nextIndex + 1,
            TokensUsed = response.OutputTokens
        };

        await _repo.AddConversationTurnAsync(userTurn, ct);
        await _repo.AddConversationTurnAsync(assistantTurn, ct);

        doc.TokensUsed += response.InputTokens + response.OutputTokens;
        await _repo.UpdateAsync(doc, ct);

        return assistantTurn;
    }

    public async Task SyncToAdoAsync(Guid requirementId, string adoProject, CancellationToken ct = default)
    {
        var doc = await _repo.GetByIdAsync(requirementId, ct)
            ?? throw new KeyNotFoundException($"RequirementDocument {requirementId} not found");

        var description = $"""
            <b>Summary:</b> {doc.Summary}<br><br>
            <b>Effort:</b> {doc.EffortEstimate?.StoryPoints} SP ({doc.EffortEstimate?.EstimatedDaysLow}-{doc.EffortEstimate?.EstimatedDaysHigh} days)<br><br>
            <b>Acceptance Criteria:</b><ul>
            {string.Join("", doc.AcceptanceCriteria.Select(ac => $"<li>{ac.Description}</li>"))}
            </ul>
            <b>Risk Flags:</b> {doc.RiskFlags.Count}<br>
            <i>Generated by PDLC AI — model: {doc.ModelVersion}</i>
            """;

        var tags = new[] { "pdlc-generated", "ai-requirements" };
        var workItem = await _ado.CreateWorkItemAsync(adoProject, AdoWorkItemType.UserStory, doc.Title, description, tags, ct);

        // Create child tasks from acceptance criteria
        var taskTitles = doc.AcceptanceCriteria
            .OrderBy(ac => ac.OrderIndex)
            .Select(ac => $"Implement: {ac.Description.Truncate(80)}")
            .ToList();

        if (taskTitles.Any())
            await _ado.AddChildTasksAsync(workItem.Id, adoProject, taskTitles, ct);

        doc.AdoWorkItemId = workItem.Id;
        doc.AdoWorkItemUrl = workItem.Url;
        doc.AdoProject = adoProject;
        await _repo.UpdateAsync(doc, ct);

        _logger.LogInformation("[Stage1] Synced to ADO — Work Item #{Id}", workItem.Id);
    }

    // ── JSON parsing helpers ──────────────────────────────────────────────────

    private static StructuredRequirementOutput ParseStructuredRequirement(string json)
    {
        try
        {
            // Claude wraps JSON in ```json ... ``` — strip fences
            var clean = json
                .Replace("```json", string.Empty)
                .Replace("```", string.Empty)
                .Trim();

            return JsonSerializer.Deserialize<StructuredRequirementOutput>(clean,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new StructuredRequirementOutput();
        }
        catch (JsonException)
        {
            // Fallback: return what we can
            return new StructuredRequirementOutput { Summary = json };
        }
    }

    // ── Output shape matching the Stage 1 prompt's JSON contract ─────────────

    private sealed class StructuredRequirementOutput
    {
        public string Title { get; set; } = "Untitled Requirement";
        public string Summary { get; set; } = string.Empty;
        public List<string> AcceptanceCriteria { get; set; } = new();
        public List<EdgeCaseItem> EdgeCases { get; set; } = new();
        public List<DependencyItem> Dependencies { get; set; } = new();
        public List<RiskItem> Risks { get; set; } = new();
        public EffortItem Effort { get; set; } = new();
    }

    private sealed class EdgeCaseItem  { public string Description { get; set; } = string.Empty; public string Severity { get; set; } = "medium"; }
    private sealed class DependencyItem { public string Name { get; set; } = string.Empty; public string Type { get; set; } = string.Empty; public string? Notes { get; set; } }
    private sealed class RiskItem      { public string Description { get; set; } = string.Empty; public string Severity { get; set; } = "medium"; public string? Mitigation { get; set; } }
    private sealed class EffortItem    { public int StoryPoints { get; set; } = 3; public string Confidence { get; set; } = "medium"; public int DaysLow { get; set; } = 1; public int DaysHigh { get; set; } = 3; public string? Rationale { get; set; } }
}

internal static class StringExtensions
{
    public static string Truncate(this string s, int maxLength) =>
        s.Length <= maxLength ? s : s[..maxLength] + "…";
}
