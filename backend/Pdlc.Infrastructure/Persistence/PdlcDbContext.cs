using Microsoft.EntityFrameworkCore;
using Pdlc.Domain.Entities;

namespace Pdlc.Infrastructure.Persistence;

public class PdlcDbContext : DbContext
{
    public PdlcDbContext(DbContextOptions<PdlcDbContext> options) : base(options) { }

    // Stage 1
    public DbSet<RequirementDocument> RequirementDocuments => Set<RequirementDocument>();
    public DbSet<AcceptanceCriterion> AcceptanceCriteria => Set<AcceptanceCriterion>();
    public DbSet<EdgeCase> EdgeCases => Set<EdgeCase>();
    public DbSet<Dependency> Dependencies => Set<Dependency>();
    public DbSet<RiskFlag> RiskFlags => Set<RiskFlag>();
    public DbSet<EffortEstimate> EffortEstimates => Set<EffortEstimate>();
    public DbSet<ConversationTurn> ConversationTurns => Set<ConversationTurn>();

    // Stage 2
    public DbSet<DesignArtifact> DesignArtifacts => Set<DesignArtifact>();

    // Stage 3
    public DbSet<CodeGenResult> CodeGenResults => Set<CodeGenResult>();
    public DbSet<PrReviewResult> PrReviewResults => Set<PrReviewResult>();

    // Stage 4
    public DbSet<TestGenResult> TestGenResults => Set<TestGenResult>();

    // Observability
    public DbSet<ClaudeUsageLog> UsageLogs => Set<ClaudeUsageLog>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // RequirementDocument
        mb.Entity<RequirementDocument>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.AdoWorkItemId);
            e.HasMany(x => x.AcceptanceCriteria).WithOne().HasForeignKey(x => x.RequirementDocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.EdgeCases).WithOne().HasForeignKey(x => x.RequirementDocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Dependencies).WithOne().HasForeignKey(x => x.RequirementDocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.RiskFlags).WithOne().HasForeignKey(x => x.RequirementDocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.EffortEstimate).WithOne().HasForeignKey<EffortEstimate>(x => x.RequirementDocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ConversationHistory).WithOne().HasForeignKey(x => x.RequirementDocumentId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.DesignArtifacts).WithOne(x => x.RequirementDocument).HasForeignKey(x => x.RequirementDocumentId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<ConversationTurn>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RequirementDocumentId, x.TurnIndex });
        });

        mb.Entity<DesignArtifact>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RequirementDocumentId, x.Type });
        });

        mb.Entity<CodeGenResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RequirementDocumentId);
        });

        mb.Entity<PrReviewResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AdoProject, x.Repository, x.PullRequestId });
        });

        mb.Entity<TestGenResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RequirementDocumentId);
        });

        mb.Entity<ClaudeUsageLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Stage);
            e.HasIndex(x => x.CreatedAt);
        });

        // Apply consistent timestamp behaviour
        foreach (var entityType in mb.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                mb.Entity(entityType.ClrType)
                  .Property(nameof(BaseEntity.CreatedAt))
                  .HasDefaultValueSql("now()");
            }
        }
    }
}
