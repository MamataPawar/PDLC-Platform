using System.Net.Http.Json;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Infrastructure.Ado;

public sealed class GitHubReposService
{
    private readonly HttpClient _http;

    public GitHubReposService(HttpClient http)
    {
        _http = http;
    }

public async Task<string> GetPullRequestDiffAsync(
    string project,      // GitHub owner
    string repository,   // GitHub repo name
    int pullRequestId,
    CancellationToken ct = default)
{
    // GitHub PR diff endpoint
    var url = $"https://api.github.com/repos/{project}/{repository}/pulls/{pullRequestId}/files";

    using var request = new HttpRequestMessage(HttpMethod.Get, url);
    request.Headers.Add("Accept", "application/vnd.github.v3+json");
    request.Headers.Add("User-Agent", "pdlc-ai-review");

    var response = await _http.SendAsync(request, ct);
    response.EnsureSuccessStatusCode();

    var files = await response.Content
        .ReadFromJsonAsync<List<GitHubFileChange>>(cancellationToken: ct) ?? [];

    var sb = new System.Text.StringBuilder();
    foreach (var file in files.Take(20))
    {
        sb.AppendLine($"--- {file.Filename} ({file.Status}) ---");
        if (!string.IsNullOrEmpty(file.Patch))
            sb.AppendLine(file.Patch);
        sb.AppendLine();
    }
    return sb.ToString();
}

public async Task PostPrCommentAsync(
    string project,
    string repository,
    int pullRequestId,
    PrComment comment,
    CancellationToken ct = default)
{
    // Post review comment to GitHub PR
    var url = $"https://api.github.com/repos/{project}/{repository}/issues/{pullRequestId}/comments";

    var payload = new { body = comment.Content };
    var response = await _http.PostAsJsonAsync(url, payload, ct);
    response.EnsureSuccessStatusCode();
}

private sealed class GitHubFileChange
{
    public string Filename { get; set; } = string.Empty;
    public string Status   { get; set; } = string.Empty;
    public string? Patch   { get; set; }
}
}