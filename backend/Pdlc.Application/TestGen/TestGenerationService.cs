using Microsoft.Extensions.Logging;
using Pdlc.Domain.Entities;
using Pdlc.Domain.Enums;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Application.TestGen;

public interface ITestGenerationService
{
    /// <summary>Generate xUnit (.NET) and/or Jasmine (Angular) test stubs.</summary>
    Task<IReadOnlyList<TestGenResult>> GenerateAsync(TestGenRequest request, CancellationToken ct = default);

    /// <summary>Commit generated test files to ADO Repo (post-merge pipeline trigger).</summary>
    Task CommitToAdoAsync(Guid requirementId, string adoProject, string repository, string branch, CancellationToken ct = default);
}

public record TestGenRequest(
    string SourceContent,
    string SourceFileName,
    TestGenTarget Target = TestGenTarget.Both,
    Guid? RequirementDocumentId = null,
    string CreatedBy = "system",
    string PromptVersion = "v1"
);

public sealed class TestGenerationService : ITestGenerationService
{
    private readonly IClaudeService _claude;
    private readonly IPromptRepository _prompts;
    private readonly ITestGenRepository _repo;
    private readonly IAdoReposService _adoRepos;
    private readonly ILogger<TestGenerationService> _logger;

    public TestGenerationService(
        IClaudeService claude,
        IPromptRepository prompts,
        ITestGenRepository repo,
        IAdoReposService adoRepos,
        ILogger<TestGenerationService> logger)
    {
        _claude = claude;
        _prompts = prompts;
        _repo = repo;
        _adoRepos = adoRepos;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TestGenResult>> GenerateAsync(
        TestGenRequest request, CancellationToken ct = default)
    {
        var systemPrompt = await _prompts.GetPromptAsync(PdlcStage.TestGen, request.PromptVersion, ct);
        var results = new List<TestGenResult>();

        if (request.Target is TestGenTarget.DotNet or TestGenTarget.Both)
        {
            var xunit = await GenerateXUnitAsync(request, systemPrompt, ct);
            results.Add(xunit);
        }

        if (request.Target is TestGenTarget.Angular or TestGenTarget.Both)
        {
            var jasmine = await GenerateJasmineAsync(request, systemPrompt, ct);
            results.Add(jasmine);
        }

        _logger.LogInformation("[Stage4] Generated {Count} test files for {Source}", results.Count, request.SourceFileName);
        return results;
    }

    public async Task CommitToAdoAsync(
        Guid requirementId, string adoProject, string repository, string branch, CancellationToken ct = default)
    {
        var results = await _repo.GetByRequirementIdAsync(requirementId, ct);
        foreach (var result in results.Where(r => r.AdoCommitUrl is null))
        {
            var outputPath = $"tests/generated/{result.OutputFileName}";
            var commitUrl = await _adoRepos.CommitFileAsync(
                adoProject, repository, branch,
                outputPath, result.GeneratedContent,
                $"test: AI-generated test stubs for {result.SourceFile} [pdlc-testgen]",
                ct);

            result.AdoCommitUrl = commitUrl;
            _logger.LogInformation("[Stage4] Committed test file {File} → {Url}", outputPath, commitUrl);
        }
    }

    // ── xUnit (.NET) ──────────────────────────────────────────────────────────

    private async Task<TestGenResult> GenerateXUnitAsync(
        TestGenRequest request, string systemPrompt, CancellationToken ct)
    {
        var userMessage = $"""
            Generate a comprehensive xUnit test class for the following C# source file.

            Source file: {request.SourceFileName}
            ```csharp
            {request.SourceContent.Truncate(6000)}
            ```

            Requirements:
            - Use xUnit with FluentAssertions
            - Use Moq for mocking dependencies
            - Cover: happy path, null/empty inputs, boundary values, async cancellation
            - Each test method: [Fact] or [Theory] with [InlineData]
            - Naming: MethodName_Scenario_ExpectedBehaviour
            - Include [Trait("Category", "Unit")] on the class
            - Add arrange/act/assert comments
            - Mock all external dependencies (HttpClient, DbContext, etc.)
            - Return ONLY the C# file content, no explanation
            """;

        var response = await _claude.SendAsync(new ClaudeRequest(
            SystemPrompt: systemPrompt,
            UserMessage: userMessage,
            Stage: PdlcStage.TestGen,
            OperationName: "XUnitTestGen"), ct);

        var outputFileName = Path.GetFileNameWithoutExtension(request.SourceFileName) + "Tests.cs";

        return await _repo.AddAsync(new TestGenResult
        {
            RequirementDocumentId = request.RequirementDocumentId,
            SourceFile = request.SourceFileName,
            Target = TestGenTarget.DotNet,
            GeneratedContent = StripCodeFences(response.Content),
            OutputFileName = outputFileName,
            TokensUsed = response.InputTokens + response.OutputTokens,
            ModelVersion = response.Model,
            CreatedBy = request.CreatedBy
        }, ct);
    }

    // ── Jasmine/Karma (Angular) ───────────────────────────────────────────────

    private async Task<TestGenResult> GenerateJasmineAsync(
        TestGenRequest request, string systemPrompt, CancellationToken ct)
    {
        var userMessage = $"""
            Generate a comprehensive Jasmine/Karma spec file for the following Angular TypeScript source.

            Source file: {request.SourceFileName}
            ```typescript
            {request.SourceContent.Truncate(6000)}
            ```

            Requirements:
            - Use Jasmine (describe/it/expect) with Angular TestBed
            - Use HttpClientTestingModule for HTTP calls
            - Cover: component creation, public method outputs, input bindings, error handling
            - Each spec: clear "should ..." description
            - Mock services using jasmine.createSpyObj()
            - Include beforeEach with TestBed.configureTestingModule
            - Test async operations with fakeAsync/tick or done callback
            - Return ONLY the TypeScript spec file content, no explanation
            """;

        var response = await _claude.SendAsync(new ClaudeRequest(
            SystemPrompt: systemPrompt,
            UserMessage: userMessage,
            Stage: PdlcStage.TestGen,
            OperationName: "JasmineTestGen"), ct);

        var outputFileName = Path.GetFileNameWithoutExtension(request.SourceFileName) + ".spec.ts";

        return await _repo.AddAsync(new TestGenResult
        {
            RequirementDocumentId = request.RequirementDocumentId,
            SourceFile = request.SourceFileName,
            Target = TestGenTarget.Angular,
            GeneratedContent = StripCodeFences(response.Content),
            OutputFileName = outputFileName,
            TokensUsed = response.InputTokens + response.OutputTokens,
            ModelVersion = response.Model,
            CreatedBy = request.CreatedBy
        }, ct);
    }

    private static string StripCodeFences(string content) =>
        content
            .Replace("```csharp", string.Empty)
            .Replace("```typescript", string.Empty)
            .Replace("```", string.Empty)
            .Trim();
}

internal static class StringExtensions
{
    public static string Truncate(this string s, int max) =>
        s.Length <= max ? s : s[..max] + "\n// [truncated for token limit]";
}
