using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pdlc.Domain.Entities;
using Pdlc.Domain.Enums;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Application.CodeGen;

public interface ICodeGenerationService
{
    /// <summary>Generate .NET Core + Angular boilerplate from a requirement + design artifact.</summary>
    Task<IReadOnlyList<CodeGenResult>> GenerateAsync(CodeGenRequest request, CancellationToken ct = default);

    /// <summary>Commit generated files to ADO Repo on a new branch (extension point).</summary>
    Task CommitToAdoAsync(Guid codeGenResultId, string adoProject, string repository, string branch, CancellationToken ct = default);
}

public record CodeGenRequest(
    Guid RequirementDocumentId,
    Guid? DesignArtifactId = null,
    CodeGenLanguage Language = CodeGenLanguage.Both,
    string CreatedBy = "system",
    string PromptVersion = "v1"
);

public sealed class CodeGenerationService : ICodeGenerationService
{
    private readonly IClaudeService _claude;
    private readonly IPromptRepository _prompts;
    private readonly IRequirementRepository _requirementRepo;
    private readonly IDesignArtifactRepository _designRepo;
    private readonly ICodeGenRepository _codeGenRepo;
    private readonly IAdoReposService _adoRepos;
    private readonly ILogger<CodeGenerationService> _logger;

    public CodeGenerationService(
        IClaudeService claude,
        IPromptRepository prompts,
        IRequirementRepository requirementRepo,
        IDesignArtifactRepository designRepo,
        ICodeGenRepository codeGenRepo,
        IAdoReposService adoRepos,
        ILogger<CodeGenerationService> logger)
    {
        _claude = claude;
        _prompts = prompts;
        _requirementRepo = requirementRepo;
        _designRepo = designRepo;
        _codeGenRepo = codeGenRepo;
        _adoRepos = adoRepos;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CodeGenResult>> GenerateAsync(
        CodeGenRequest request, CancellationToken ct = default)
    {
        var doc = await _requirementRepo.GetByIdAsync(request.RequirementDocumentId, ct)
            ?? throw new KeyNotFoundException($"RequirementDocument {request.RequirementDocumentId} not found");

        // Load design artifacts if available
        var designContext = string.Empty;
        if (request.DesignArtifactId.HasValue)
        {
            var artifact = await _designRepo.GetByIdAsync(request.DesignArtifactId.Value, ct);
            if (artifact is not null)
                designContext = $"\n\nDesign artifact ({artifact.Type}):\n{artifact.Content}";
        }
        else
        {
            var allArtifacts = await _designRepo.GetByRequirementIdAsync(doc.Id, ct);
            if (allArtifacts.Any())
                designContext = string.Join("\n\n", allArtifacts.Select(a => $"--- {a.Type} ---\n{a.Content}"));
        }

        var systemPrompt = await _prompts.GetPromptAsync(PdlcStage.CodeGen, request.PromptVersion, ct);
        var results = new List<CodeGenResult>();

        if (request.Language is CodeGenLanguage.CSharp or CodeGenLanguage.Both)
        {
            var dotnetResult = await GenerateDotNetAsync(doc, designContext, systemPrompt, request, ct);
            results.Add(dotnetResult);
        }

        if (request.Language is CodeGenLanguage.Angular or CodeGenLanguage.Both)
        {
            var angularResult = await GenerateAngularAsync(doc, designContext, systemPrompt, request, ct);
            results.Add(angularResult);
        }

        _logger.LogInformation("[Stage3a] Generated {Count} code files for requirement {Id}", results.Count, doc.Id);
        return results;
    }

    public async Task CommitToAdoAsync(
        Guid codeGenResultId, string adoProject, string repository, string branch, CancellationToken ct = default)
    {
        // Retrieve all CodeGenResults for this session
        // In a real scenario, codeGenResultId maps to a group of files
        var results = await _codeGenRepo.GetByRequirementIdAsync(codeGenResultId, ct);
        foreach (var result in results.Where(r => r.AdoCommitUrl is null))
        {
            var commitUrl = await _adoRepos.CommitFileAsync(
                adoProject, repository, branch,
                result.FileName, result.GeneratedContent,
                $"feat: AI-generated scaffold for {result.FileName} [pdlc-codegen]",
                ct);

            result.AdoCommitUrl = commitUrl;
            result.AdoBranch = branch;
            await _codeGenRepo.UpdateAsync(result, ct);
            _logger.LogInformation("[Stage3a] Committed {File} → {Url}", result.FileName, commitUrl);
        }
    }

    // ── .NET Core generation ──────────────────────────────────────────────────

    private async Task<CodeGenResult> GenerateDotNetAsync(
        RequirementDocument doc,
        string designContext,
        string systemPrompt,
        CodeGenRequest request,
        CancellationToken ct)
    {
        var userMessage = $"""
            Generate a complete .NET Core C# scaffold for the following requirement.
            Produce a single JSON object with keys: controller, service, repository, domainEntity.
            Each value is the full file content as a string.

            Requirement title: {doc.Title}
            Summary: {doc.Summary}

            Acceptance criteria:
            {string.Join("\n", doc.AcceptanceCriteria.Select((ac, i) => $"{i + 1}. {ac.Description}"))}
            {designContext}

            Rules:
            - Follow clean architecture (Controller → Service → Repository pattern)
            - Use async/await throughout
            - Add XML doc comments on public members
            - Use dependency injection via constructor
            - Controller route: api/[entity-name-plural]
            - Include basic input validation
            """;

        var response = await _claude.SendAsync(new ClaudeRequest(
            SystemPrompt: systemPrompt,
            UserMessage: userMessage,
            Stage: PdlcStage.CodeGen,
            OperationName: "DotNetCodeGen"), ct);

        var parsed = ParseCodeBlocks(response.Content);
        var aggregated = FormatGeneratedFiles(parsed, "dotnet");

        return await _codeGenRepo.AddAsync(new CodeGenResult
        {
            RequirementDocumentId = doc.Id,
            Language = CodeGenLanguage.CSharp,
            GeneratedContent = aggregated,
            FileName = $"{Slugify(doc.Title)}.dotnet.generated.cs",
            TokensUsed = response.InputTokens + response.OutputTokens,
            ModelVersion = response.Model,
            CreatedBy = request.CreatedBy
        }, ct);
    }

    // ── Angular generation ────────────────────────────────────────────────────

    private async Task<CodeGenResult> GenerateAngularAsync(
        RequirementDocument doc,
        string designContext,
        string systemPrompt,
        CodeGenRequest request,
        CancellationToken ct)
    {
        var userMessage = $"""
            Generate a complete Angular 19 TypeScript scaffold for the following requirement.
            Produce a single JSON object with keys: component, service, model, routingModule.
            Each value is the full file content as a string.

            Requirement title: {doc.Title}
            Summary: {doc.Summary}

            Acceptance criteria:
            {string.Join("\n", doc.AcceptanceCriteria.Select((ac, i) => $"{i + 1}. {ac.Description}"))}
            {designContext}

            Rules:
            - Use standalone components (Angular 19 style)
            - Use HttpClient for API calls with typed responses
            - Use reactive forms where user input is required
            - Include loading and error state handling
            - Add JSDoc comments on public methods
            - Service uses inject() instead of constructor injection
            """;

        var response = await _claude.SendAsync(new ClaudeRequest(
            SystemPrompt: systemPrompt,
            UserMessage: userMessage,
            Stage: PdlcStage.CodeGen,
            OperationName: "AngularCodeGen"), ct);

        var parsed = ParseCodeBlocks(response.Content);
        var aggregated = FormatGeneratedFiles(parsed, "angular");

        return await _codeGenRepo.AddAsync(new CodeGenResult
        {
            RequirementDocumentId = doc.Id,
            Language = CodeGenLanguage.Angular,
            GeneratedContent = aggregated,
            FileName = $"{Slugify(doc.Title)}.angular.generated.ts",
            TokensUsed = response.InputTokens + response.OutputTokens,
            ModelVersion = response.Model,
            CreatedBy = request.CreatedBy
        }, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, string> ParseCodeBlocks(string content)
    {
        try
        {
            var clean = content
                .Replace("```json", string.Empty)
                .Replace("```", string.Empty)
                .Trim();
            return JsonSerializer.Deserialize<Dictionary<string, string>>(clean,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string> { ["raw"] = content };
        }
    }

    private static string FormatGeneratedFiles(Dictionary<string, string> blocks, string stack)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"// ═══════════════════════════════════════════════════");
        sb.AppendLine($"// AI-GENERATED SCAFFOLD [{stack.ToUpperInvariant()}]");
        sb.AppendLine($"// Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine($"// ⚠️  Review before committing — treat as a starting point");
        sb.AppendLine($"// ═══════════════════════════════════════════════════");
        sb.AppendLine();
        foreach (var (key, code) in blocks)
        {
            sb.AppendLine($"// ── {key.ToUpperInvariant()} ─────────────────────────────");
            sb.AppendLine(code);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string Slugify(string title) =>
        System.Text.RegularExpressions.Regex.Replace(title.ToLower(), @"[^a-z0-9]+", "-").Trim('-');
}
