using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pdlc.Domain.Enums;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Infrastructure.Prompts;

/// <summary>
/// Loads system prompts from the /ai-prompts/ directory at runtime.
/// Prompts are versioned and resolved by stage + version string.
/// </summary>
public sealed class FilePromptRepository : IPromptRepository
{
    private readonly PromptOptions _options;
    private readonly ILogger<FilePromptRepository> _logger;

    // Simple in-memory cache: (stage, version) → prompt text
    private readonly Dictionary<(PdlcStage, string), string> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly Dictionary<PdlcStage, string> StageFileNames = new()
    {
        [PdlcStage.Requirements] = "stage1-requirements.md",
        [PdlcStage.Design]       = "stage2-architecture.md",
        [PdlcStage.CodeGen]      = "stage3a-codegen.md",
        [PdlcStage.PrReview]     = "stage3b-pr-review.md",
        [PdlcStage.TestGen]      = "stage4-testgen.md",
    };

    public FilePromptRepository(IOptions<PromptOptions> options, ILogger<FilePromptRepository> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetPromptAsync(PdlcStage stage, string version = "v1", CancellationToken ct = default)
    {
        var cacheKey = (stage, version);

        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            if (!StageFileNames.TryGetValue(stage, out var fileName))
                throw new ArgumentException($"No prompt file registered for stage {stage}");

            var path = Path.Combine(_options.PromptsBasePath, version, fileName);

            if (!File.Exists(path))
            {
                _logger.LogWarning("[Prompts] File not found at {Path} — falling back to v1", path);
                path = Path.Combine(_options.PromptsBasePath, "v1", fileName);
            }

            if (!File.Exists(path))
                throw new FileNotFoundException($"System prompt not found: {path}");

            var content = await File.ReadAllTextAsync(path, ct);
            _cache[cacheKey] = content;

            _logger.LogInformation("[Prompts] Loaded {Stage} v{Version} ({Chars} chars)", stage, version, content.Length);
            return content;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<IReadOnlyList<string>> ListVersionsAsync(PdlcStage stage, CancellationToken ct = default)
    {
        if (!StageFileNames.TryGetValue(stage, out var fileName))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var versions = Directory.GetDirectories(_options.PromptsBasePath)
            .Where(d => File.Exists(Path.Combine(d, fileName)))
            .Select(d => Path.GetFileName(d))
            .OrderBy(v => v)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(versions);
    }
}

public sealed class PromptOptions
{
    public const string Section = "Prompts";
    public string PromptsBasePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "../../../../ai-prompts");
}
