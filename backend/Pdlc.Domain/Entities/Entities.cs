using Pdlc.Domain.Enums;

namespace Pdlc.Domain.Entities;

/// <summary>Base entity with audit fields.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "system";
}

// ── Stage 1 ──────────────────────────────────────────────────────────────────

/// <summary>
/// Persisted output of a Stage 1 requirement analysis session.
/// One RequirementDocument per raw input submission.
/// </summary>
public class RequirementDocument : BaseEntity
{
    public string RawInput { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public RequirementStatus Status { get; set; } = RequirementStatus.Draft;
    public string? AdoWorkItemUrl { get; set; }
    public int? AdoWorkItemId { get; set; }
    public string? AdoProject { get; set; }
    public int TokensUsed { get; set; }
    public string ModelVersion { get; set; } = string.Empty;

    public ICollection<AcceptanceCriterion> AcceptanceCriteria { get; set; } = new List<AcceptanceCriterion>();
    public ICollection<EdgeCase> EdgeCases { get; set; } = new List<EdgeCase>();
    public ICollection<Dependency> Dependencies { get; set; } = new List<Dependency>();
    public ICollection<RiskFlag> RiskFlags { get; set; } = new List<RiskFlag>();
    public EffortEstimate? EffortEstimate { get; set; }
    public ICollection<ConversationTurn> ConversationHistory { get; set; } = new List<ConversationTurn>();
    public ICollection<DesignArtifact> DesignArtifacts { get; set; } = new List<DesignArtifact>();
}

public class AcceptanceCriterion : BaseEntity
{
    public Guid RequirementDocumentId { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsTestable { get; set; } = true;
    public int OrderIndex { get; set; }
}

public class EdgeCase : BaseEntity
{
    public Guid RequirementDocumentId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium"; // low | medium | high
    public string? MitigationNote { get; set; }
}

public class Dependency : BaseEntity
{
    public Guid RequirementDocumentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // service | team | external | data
    public string? Notes { get; set; }
}

public class RiskFlag : BaseEntity
{
    public Guid RequirementDocumentId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public string? Mitigation { get; set; }
}

public class EffortEstimate : BaseEntity
{
    public Guid RequirementDocumentId { get; set; }
    public int StoryPoints { get; set; }
    public string Confidence { get; set; } = "medium"; // low | medium | high
    public int EstimatedDaysLow { get; set; }
    public int EstimatedDaysHigh { get; set; }
    public string? Rationale { get; set; }
}

// ── Conversation (multi-turn) ─────────────────────────────────────────────────

public class ConversationTurn : BaseEntity
{
    public Guid RequirementDocumentId { get; set; }
    public string Role { get; set; } = string.Empty;  // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
    public int TurnIndex { get; set; }
    public int? TokensUsed { get; set; }
}

// ── Stage 2 ──────────────────────────────────────────────────────────────────

public class DesignArtifact : BaseEntity
{
    public Guid RequirementDocumentId { get; set; }
    public DesignArtifactType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;  // JSON or YAML string
    public string Format { get; set; } = "json";          // json | yaml | markdown
    public int TokensUsed { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public RequirementDocument? RequirementDocument { get; set; }
}

// ── Stage 3 ──────────────────────────────────────────────────────────────────

public class CodeGenResult : BaseEntity
{
    public Guid RequirementDocumentId { get; set; }
    public Guid? DesignArtifactId { get; set; }
    public CodeGenLanguage Language { get; set; }
    public string GeneratedContent { get; set; } = string.Empty;  // raw code or zip base64
    public string FileName { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public string? AdoCommitUrl { get; set; }   // populated when committed via ADO
    public string? AdoBranch { get; set; }
}

public class PrReviewResult : BaseEntity
{
    public string AdoProject { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public int PullRequestId { get; set; }
    public string DiffSummary { get; set; } = string.Empty;
    public string ReviewJson { get; set; } = string.Empty;   // serialised list of comments
    public int CommentsPosted { get; set; }
    public int TokensUsed { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
}

// ── Stage 4 ──────────────────────────────────────────────────────────────────

public class TestGenResult : BaseEntity
{
    public Guid? RequirementDocumentId { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public TestGenTarget Target { get; set; }
    public string GeneratedContent { get; set; } = string.Empty;
    public string OutputFileName { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public string? AdoCommitUrl { get; set; }
}

// ── Token Usage Log ───────────────────────────────────────────────────────────

public class ClaudeUsageLog : BaseEntity
{
    public PdlcStage Stage { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public long DurationMs { get; set; }
    public bool IsStreamed { get; set; }
    public string? CorrelationId { get; set; }
}
