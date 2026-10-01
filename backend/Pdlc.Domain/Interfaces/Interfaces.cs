using Pdlc.Domain.Entities;
using Pdlc.Domain.Enums;

namespace Pdlc.Domain.Interfaces;

// ── Claude AI ─────────────────────────────────────────────────────────────────

public interface IClaudeService
{
    /// <summary>Single-shot request → response.</summary>
    Task<ClaudeResponse> SendAsync(ClaudeRequest request, CancellationToken ct = default);

    /// <summary>Streaming request — yields text chunks as they arrive.</summary>
    IAsyncEnumerable<string> StreamAsync(ClaudeRequest request, CancellationToken ct = default);

    /// <summary>Continue an existing multi-turn conversation.</summary>
    Task<ClaudeResponse> ContinueConversationAsync(
        IEnumerable<ConversationTurn> history,
        string newUserMessage,
        string systemPrompt,
        CancellationToken ct = default);
}

public record ClaudeRequest(
    string SystemPrompt,
    string UserMessage,
    string? ConversationId = null,
    int MaxTokens = 4096,
    bool Stream = false,
    PdlcStage? Stage = null,
    string? OperationName = null
);

public record ClaudeResponse(
    string Content,
    int InputTokens,
    int OutputTokens,
    string Model,
    string? ConversationId = null
);

// ── Prompt Repository ─────────────────────────────────────────────────────────

public interface IPromptRepository
{
    /// <summary>Load a versioned system prompt by stage and version.</summary>
    Task<string> GetPromptAsync(PdlcStage stage, string version = "v1", CancellationToken ct = default);

    /// <summary>List available versions for a stage.</summary>
    Task<IReadOnlyList<string>> ListVersionsAsync(PdlcStage stage, CancellationToken ct = default);
}

// ── Persistence ───────────────────────────────────────────────────────────────

public interface IRequirementRepository
{
    Task<RequirementDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RequirementDocument>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<RequirementDocument> AddAsync(RequirementDocument doc, CancellationToken ct = default);
    Task UpdateAsync(RequirementDocument doc, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationTurn>> GetConversationAsync(Guid requirementId, CancellationToken ct = default);
    Task AddConversationTurnAsync(ConversationTurn turn, CancellationToken ct = default);
}

public interface IDesignArtifactRepository
{
    Task<DesignArtifact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DesignArtifact>> GetByRequirementIdAsync(Guid requirementId, CancellationToken ct = default);
    Task<DesignArtifact> AddAsync(DesignArtifact artifact, CancellationToken ct = default);
}

public interface ICodeGenRepository
{
    Task<CodeGenResult> AddAsync(CodeGenResult result, CancellationToken ct = default);
    Task<IReadOnlyList<CodeGenResult>> GetByRequirementIdAsync(Guid requirementId, CancellationToken ct = default);
    Task UpdateAsync(CodeGenResult result, CancellationToken ct = default);
}

public interface ITestGenRepository
{
    Task<TestGenResult> AddAsync(TestGenResult result, CancellationToken ct = default);
    Task<IReadOnlyList<TestGenResult>> GetByRequirementIdAsync(Guid requirementId, CancellationToken ct = default);
}

public interface IPrReviewRepository
{
    Task<PrReviewResult> AddAsync(PrReviewResult result, CancellationToken ct = default);
    Task<PrReviewResult?> GetByPrIdAsync(string adoProject, string repo, int prId, CancellationToken ct = default);
}

public interface IUsageLogRepository
{
    Task AddAsync(ClaudeUsageLog log, CancellationToken ct = default);
    Task<IReadOnlyList<ClaudeUsageLog>> GetByStageAsync(PdlcStage stage, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

// ── ADO Integration ───────────────────────────────────────────────────────────

public interface IAdoBoardsService
{
    Task<AdoWorkItemResult> CreateWorkItemAsync(
        string project,
        AdoWorkItemType type,
        string title,
        string description,
        IEnumerable<string>? tags = null,
        CancellationToken ct = default);

    Task<AdoWorkItemResult> UpdateWorkItemAsync(
        int workItemId,
        string project,
        string? title = null,
        string? description = null,
        string? state = null,
        CancellationToken ct = default);

    Task AddChildTasksAsync(
        int parentWorkItemId,
        string project,
        IEnumerable<string> taskTitles,
        CancellationToken ct = default);
}

public interface IAdoReposService
{
    Task<string> GetPullRequestDiffAsync(
        string project,
        string repository,
        int pullRequestId,
        CancellationToken ct = default);

    Task PostPrCommentAsync(
        string project,
        string repository,
        int pullRequestId,
        PrComment comment,
        CancellationToken ct = default);

    Task<string> CommitFileAsync(
        string project,
        string repository,
        string branch,
        string filePath,
        string content,
        string commitMessage,
        CancellationToken ct = default);
}

// ── Value Objects ─────────────────────────────────────────────────────────────

public record AdoWorkItemResult(int Id, string Url, string State);

public record PrComment(
    string FilePath,
    int? LineNumber,
    string Content,
    string CommentType  // "text" | "codeChange"
);
