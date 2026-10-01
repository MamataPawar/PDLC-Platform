using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Infrastructure.Ado;

public sealed class AdoReposService : IAdoReposService
{
    private readonly HttpClient _http;
    private readonly AdoOptions _options;
    private readonly ILogger<AdoReposService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AdoReposService(HttpClient http, IOptions<AdoOptions> options, ILogger<AdoReposService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    // ── PR Diff ───────────────────────────────────────────────────────────────

    public async Task<string> GetPullRequestDiffAsync(
        string project,
        string repository,
        int pullRequestId,
        CancellationToken ct = default)
    {
        // Fetch PR iterations to get the latest iteration ID
        var iterUrl = $"{project}/_apis/git/repositories/{repository}/pullRequests/{pullRequestId}/iterations?api-version=7.1";
        var iterResponse = await _http.GetFromJsonAsync<AdoIterationsResponse>(iterUrl, JsonOpts, ct)
            ?? throw new InvalidOperationException("Failed to fetch PR iterations");

        var latestIteration = iterResponse.Value.MaxBy(i => i.Id);
        if (latestIteration is null) return string.Empty;

        // Fetch changes for the latest iteration
        var changesUrl = $"{project}/_apis/git/repositories/{repository}/pullRequests/{pullRequestId}/iterations/{latestIteration.Id}/changes?api-version=7.1";
        var changesResponse = await _http.GetFromJsonAsync<AdoChangesResponse>(changesUrl, JsonOpts, ct)
            ?? throw new InvalidOperationException("Failed to fetch PR changes");

        // Collect diff content from each changed file
        var diffBuilder = new System.Text.StringBuilder();
        foreach (var change in changesResponse.ChangeEntries.Take(20)) // cap at 20 files
        {
            if (change.Item?.Path is null) continue;
            diffBuilder.AppendLine($"--- {change.Item.Path} ({change.ChangeType}) ---");

            try
            {
                var contentUrl = $"{project}/_apis/git/repositories/{repository}/items?path={Uri.EscapeDataString(change.Item.Path)}&versionDescriptor.version={latestIteration.SourceRefCommit?.CommitId}&api-version=7.1";
                var content = await _http.GetStringAsync(contentUrl, ct);
                var preview = string.Join('\n', content.Split('\n').Take(100));
                diffBuilder.AppendLine(preview);
                diffBuilder.AppendLine();
            }
            catch (HttpRequestException)
            {
                diffBuilder.AppendLine("[File content unavailable — may be binary or deleted]");
            }
        }

        return diffBuilder.ToString();
    }

    // ── PR Comments ───────────────────────────────────────────────────────────

    public async Task PostPrCommentAsync(
        string project,
        string repository,
        int pullRequestId,
        PrComment comment,
        CancellationToken ct = default)
    {
        var payload = new
        {
            comments = new[]
            {
                new { parentCommentId = 0, content = comment.Content, commentType = 1 }
            },
            status = "active",
            threadContext = comment.FilePath is not null ? new
            {
                filePath = comment.FilePath,
                rightFileStart = comment.LineNumber.HasValue ? new { line = comment.LineNumber, offset = 1 } : null,
                rightFileEnd  = comment.LineNumber.HasValue ? new { line = comment.LineNumber, offset = 1 } : null
            } : null
        };

        var url = $"{project}/_apis/git/repositories/{repository}/pullRequests/{pullRequestId}/threads?api-version=7.1";
        var response = await _http.PostAsJsonAsync(url, payload, JsonOpts, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("[ADO Repos] Failed to post comment on PR {PrId}: {Status} {Body}", pullRequestId, response.StatusCode, body);
        }
    }

    // ── File Commit (ADO commit extension point) ──────────────────────────────

    public async Task<string> CommitFileAsync(
        string project,
        string repository,
        string branch,
        string filePath,
        string content,
        string commitMessage,
        CancellationToken ct = default)
    {
        // Get latest commit on branch
        var refUrl = $"{project}/_apis/git/repositories/{repository}/refs?filter=heads/{branch}&api-version=7.1";
        var refResponse = await _http.GetFromJsonAsync<AdoRefsResponse>(refUrl, JsonOpts, ct);
        var oldObjectId = refResponse?.Value?.FirstOrDefault()?.ObjectId ?? "0000000000000000000000000000000000000000";

        var push = new
        {
            refUpdates = new[] { new { name = $"refs/heads/{branch}", oldObjectId } },
            commits = new[]
            {
                new
                {
                    comment = commitMessage,
                    changes = new[]
                    {
                        new
                        {
                            changeType = "add",
                            item = new { path = filePath },
                            newContent = new { content, contentType = "rawtext" }
                        }
                    }
                }
            }
        };

        var pushUrl = $"{project}/_apis/git/repositories/{repository}/pushes?api-version=7.1";
        var response = await _http.PostAsJsonAsync(pushUrl, push, JsonOpts, ct);
        response.EnsureSuccessStatusCode();

        var pushResult = await response.Content.ReadFromJsonAsync<AdoPushResponse>(JsonOpts, ct);
        var commitUrl = $"https://dev.azure.com/{_options.Organisation}/{project}/_git/{repository}/commit/{pushResult?.Commits?.FirstOrDefault()?.CommitId}";

        _logger.LogInformation("[ADO Repos] Committed {File} to {Branch}: {Url}", filePath, branch, commitUrl);
        return commitUrl;
    }

    // ── Wire types ────────────────────────────────────────────────────────────

    private sealed class AdoIterationsResponse
    {
        public List<AdoIteration> Value { get; set; } = new();
    }

    private sealed class AdoIteration
    {
        public int Id { get; set; }
        public AdoCommitRef? SourceRefCommit { get; set; }
    }

    private sealed class AdoCommitRef { public string? CommitId { get; set; } }

    private sealed class AdoChangesResponse
    {
        public List<AdoChangeEntry> ChangeEntries { get; set; } = new();
    }

    private sealed class AdoChangeEntry
    {
        public AdoItem? Item { get; set; }
        public string ChangeType { get; set; } = string.Empty;
    }

    private sealed class AdoItem { public string? Path { get; set; } }

    private sealed class AdoRefsResponse
    {
        public List<AdoRef> Value { get; set; } = new();
    }

    private sealed class AdoRef { public string ObjectId { get; set; } = string.Empty; }

    private sealed class AdoPushResponse
    {
        public List<AdoCommitRef> Commits { get; set; } = new();
    }
}
