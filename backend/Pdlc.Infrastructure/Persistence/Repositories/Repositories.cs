using Microsoft.EntityFrameworkCore;
using Pdlc.Domain.Entities;
using Pdlc.Domain.Enums;
using Pdlc.Domain.Interfaces;
using Pdlc.Infrastructure.Persistence;

namespace Pdlc.Infrastructure.Persistence.Repositories;

// ── Requirements ──────────────────────────────────────────────────────────────

public class RequirementRepository : IRequirementRepository
{
    private readonly PdlcDbContext _db;
    public RequirementRepository(PdlcDbContext db) => _db = db;

    public async Task<RequirementDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.RequirementDocuments
            .Include(r => r.AcceptanceCriteria)
            .Include(r => r.EdgeCases)
            .Include(r => r.Dependencies)
            .Include(r => r.RiskFlags)
            .Include(r => r.EffortEstimate)
            .Include(r => r.ConversationHistory)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<RequirementDocument>> ListAsync(int page, int pageSize, CancellationToken ct = default) =>
        await _db.RequirementDocuments
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<RequirementDocument> AddAsync(RequirementDocument doc, CancellationToken ct = default)
    {
        _db.RequirementDocuments.Add(doc);
        await _db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task UpdateAsync(RequirementDocument doc, CancellationToken ct = default)
    {
        doc.UpdatedAt = DateTimeOffset.UtcNow;
        _db.RequirementDocuments.Update(doc);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ConversationTurn>> GetConversationAsync(Guid requirementId, CancellationToken ct = default) =>
        await _db.ConversationTurns
            .Where(t => t.RequirementDocumentId == requirementId)
            .OrderBy(t => t.TurnIndex)
            .ToListAsync(ct);

    public async Task AddConversationTurnAsync(ConversationTurn turn, CancellationToken ct = default)
    {
        _db.ConversationTurns.Add(turn);
        await _db.SaveChangesAsync(ct);
    }
}

// ── Design Artifacts ──────────────────────────────────────────────────────────

public class DesignArtifactRepository : IDesignArtifactRepository
{
    private readonly PdlcDbContext _db;
    public DesignArtifactRepository(PdlcDbContext db) => _db = db;

    public async Task<DesignArtifact?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.DesignArtifacts.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<DesignArtifact>> GetByRequirementIdAsync(Guid requirementId, CancellationToken ct = default) =>
        await _db.DesignArtifacts
            .Where(a => a.RequirementDocumentId == requirementId)
            .ToListAsync(ct);

    public async Task<DesignArtifact> AddAsync(DesignArtifact artifact, CancellationToken ct = default)
    {
        _db.DesignArtifacts.Add(artifact);
        await _db.SaveChangesAsync(ct);
        return artifact;
    }
}

// ── CodeGen ───────────────────────────────────────────────────────────────────

public class CodeGenRepository : ICodeGenRepository
{
    private readonly PdlcDbContext _db;
    public CodeGenRepository(PdlcDbContext db) => _db = db;

    public async Task<CodeGenResult> AddAsync(CodeGenResult result, CancellationToken ct = default)
    {
        _db.CodeGenResults.Add(result);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<CodeGenResult>> GetByRequirementIdAsync(Guid requirementId, CancellationToken ct = default) =>
        await _db.CodeGenResults
            .Where(r => r.RequirementDocumentId == requirementId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task UpdateAsync(CodeGenResult result, CancellationToken ct = default)
    {
        result.UpdatedAt = DateTimeOffset.UtcNow;
        _db.CodeGenResults.Update(result);
        await _db.SaveChangesAsync(ct);
    }
}

// ── TestGen ───────────────────────────────────────────────────────────────────

public class TestGenRepository : ITestGenRepository
{
    private readonly PdlcDbContext _db;
    public TestGenRepository(PdlcDbContext db) => _db = db;

    public async Task<TestGenResult> AddAsync(TestGenResult result, CancellationToken ct = default)
    {
        _db.TestGenResults.Add(result);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<TestGenResult>> GetByRequirementIdAsync(Guid requirementId, CancellationToken ct = default) =>
        await _db.TestGenResults
            .Where(r => r.RequirementDocumentId == requirementId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
}

// ── PR Review ─────────────────────────────────────────────────────────────────

public class PrReviewRepository : IPrReviewRepository
{
    private readonly PdlcDbContext _db;
    public PrReviewRepository(PdlcDbContext db) => _db = db;

    public async Task<PrReviewResult> AddAsync(PrReviewResult result, CancellationToken ct = default)
    {
        _db.PrReviewResults.Add(result);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<PrReviewResult?> GetByPrIdAsync(string adoProject, string repo, int prId, CancellationToken ct = default) =>
        await _db.PrReviewResults
            .Where(r => r.AdoProject == adoProject && r.Repository == repo && r.PullRequestId == prId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
}

// ── Usage Logs ────────────────────────────────────────────────────────────────

public class UsageLogRepository : IUsageLogRepository
{
    private readonly PdlcDbContext _db;
    public UsageLogRepository(PdlcDbContext db) => _db = db;

    public async Task AddAsync(ClaudeUsageLog log, CancellationToken ct = default)
    {
        _db.UsageLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ClaudeUsageLog>> GetByStageAsync(
        PdlcStage stage, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
        await _db.UsageLogs
            .Where(l => l.Stage == stage && l.CreatedAt >= from && l.CreatedAt <= to)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);
}
