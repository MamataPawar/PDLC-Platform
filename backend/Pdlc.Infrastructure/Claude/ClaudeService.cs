using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pdlc.Domain.Entities;
using Pdlc.Domain.Enums;
using Pdlc.Domain.Interfaces;

namespace Pdlc.Infrastructure.Claude;

/// <summary>
/// Wraps the Anthropic Claude API with:
///   - Polly-backed retry (3 attempts, exponential backoff, 429 + 5xx)
///   - Token usage logging to IUsageLogRepository
///   - Streaming support via IAsyncEnumerable
///   - Multi-turn conversation continuation
///   - Model pinned to claude-sonnet-4-6
/// </summary>
public sealed class ClaudeService : IClaudeService
{
    private const string Model = "claude-sonnet-4-6";
    private const string ApiVersion = "2023-06-01";

    private readonly HttpClient _http;
    private readonly IUsageLogRepository _usageLog;
    private readonly ILogger<ClaudeService> _logger;
    private readonly ClaudeOptions _options;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ClaudeService(
        HttpClient http,
        IUsageLogRepository usageLog,
        ILogger<ClaudeService> logger,
        IOptions<ClaudeOptions> options)
    {
        _http = http;
        _usageLog = usageLog;
        _logger = logger;
        _options = options.Value;
    }

    // ── Single-shot ───────────────────────────────────────────────────────────

    public async Task<ClaudeResponse> SendAsync(ClaudeRequest request, CancellationToken ct = default)
    {
        var payload = BuildPayload(request.SystemPrompt, new[]
        {
            new ClaudeMessage("user", request.UserMessage)
        }, stream: false);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var httpResponse = await _http.PostAsJsonAsync("/v1/messages", payload, JsonOpts, ct);
        httpResponse.EnsureSuccessStatusCode();

        var apiResponse = await httpResponse.Content.ReadFromJsonAsync<ClaudeApiResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Empty response from Claude API");

        sw.Stop();

        var content = apiResponse.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? string.Empty;
        var response = new ClaudeResponse(
            content,
            apiResponse.Usage.InputTokens,
            apiResponse.Usage.OutputTokens,
            apiResponse.Model,
            request.ConversationId);

        await LogUsageAsync(request, apiResponse.Usage, sw.ElapsedMilliseconds, isStreamed: false, ct);

        _logger.LogInformation(
            "[Claude] Stage={Stage} Op={Op} Tokens={Total} Duration={Ms}ms",
            request.Stage, request.OperationName,
            apiResponse.Usage.InputTokens + apiResponse.Usage.OutputTokens,
            sw.ElapsedMilliseconds);

        return response;
    }

    // ── Multi-turn ────────────────────────────────────────────────────────────

    public async Task<ClaudeResponse> ContinueConversationAsync(
        IEnumerable<ConversationTurn> history,
        string newUserMessage,
        string systemPrompt,
        CancellationToken ct = default)
    {
        var messages = history
            .OrderBy(t => t.TurnIndex)
            .Select(t => new ClaudeMessage(t.Role, t.Content))
            .Append(new ClaudeMessage("user", newUserMessage))
            .ToArray();

        var payload = BuildPayload(systemPrompt, messages, stream: false);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var httpResponse = await _http.PostAsJsonAsync("/v1/messages", payload, JsonOpts, ct);
        httpResponse.EnsureSuccessStatusCode();

        var apiResponse = await httpResponse.Content.ReadFromJsonAsync<ClaudeApiResponse>(JsonOpts, ct)
            ?? throw new InvalidOperationException("Empty response from Claude API");

        sw.Stop();

        var content = apiResponse.Content.FirstOrDefault(c => c.Type == "text")?.Text ?? string.Empty;

        await LogUsageAsync(
            new ClaudeRequest(systemPrompt, newUserMessage, Stage: PdlcStage.Requirements, OperationName: "ContinueConversation"),
            apiResponse.Usage, sw.ElapsedMilliseconds, isStreamed: false, ct);

        return new ClaudeResponse(content, apiResponse.Usage.InputTokens, apiResponse.Usage.OutputTokens, apiResponse.Model);
    }

    // ── Streaming ─────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<string> StreamAsync(
        ClaudeRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = BuildPayload(request.SystemPrompt, new[]
        {
            new ClaudeMessage("user", request.UserMessage)
        }, stream: true);

        var body = JsonSerializer.Serialize(payload, JsonOpts);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        int totalInput = 0, totalOutput = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") break;

            ClaudeStreamEvent? evt = null;
            try { evt = JsonSerializer.Deserialize<ClaudeStreamEvent>(data, JsonOpts); }
            catch (JsonException ex) { _logger.LogWarning(ex, "Failed to parse SSE: {Data}", data); continue; }

            if (evt is null) continue;

            if (evt.Type == "content_block_delta" && evt.Delta?.Type == "text_delta")
                yield return evt.Delta.Text ?? string.Empty;

            if (evt.Type == "message_delta" && evt.Usage is not null)
                totalOutput = evt.Usage.OutputTokens;

            if (evt.Type == "message_start" && evt.Message?.Usage is not null)
                totalInput = evt.Message.Usage.InputTokens;
        }

        sw.Stop();
        await LogUsageAsync(request,
            new UsageData { InputTokens = totalInput, OutputTokens = totalOutput },
            sw.ElapsedMilliseconds, isStreamed: true, ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static object BuildPayload(string systemPrompt, ClaudeMessage[] messages, bool stream) =>
        new
        {
            model = Model,
            max_tokens = 8192,
            stream,
            system = systemPrompt,
            messages
        };

    private async Task LogUsageAsync(
        ClaudeRequest request,
        UsageData usage,
        long elapsedMs,
        bool isStreamed,
        CancellationToken ct)
    {
        try
        {
            await _usageLog.AddAsync(new ClaudeUsageLog
            {
                Stage = request.Stage ?? PdlcStage.Requirements,
                OperationName = request.OperationName ?? "Unknown",
                Model = Model,
                InputTokens = usage.InputTokens,
                OutputTokens = usage.OutputTokens,
                TotalTokens = usage.InputTokens + usage.OutputTokens,
                DurationMs = elapsedMs,
                IsStreamed = isStreamed,
                CorrelationId = request.ConversationId
            }, ct);
        }
        catch (Exception ex)
        {
            // Never let logging failure propagate to caller
            _logger.LogWarning(ex, "Failed to persist usage log");
        }
    }

    // ── Wire types for Anthropic API JSON ─────────────────────────────────────

    private record ClaudeMessage(string Role, string Content);

    private sealed class ClaudeApiResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public List<ContentBlock> Content { get; set; } = new();
        public UsageData Usage { get; set; } = new();
    }

    private sealed class ContentBlock
    {
        public string Type { get; set; } = string.Empty;
        public string? Text { get; set; }
    }

    private sealed class UsageData
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

    private sealed class ClaudeStreamEvent
    {
        public string Type { get; set; } = string.Empty;
        public DeltaBlock? Delta { get; set; }
        public UsageData? Usage { get; set; }
        public MessageStart? Message { get; set; }
    }

    private sealed class DeltaBlock
    {
        public string Type { get; set; } = string.Empty;
        public string? Text { get; set; }
    }

    private sealed class MessageStart
    {
        public UsageData? Usage { get; set; }
    }
}

public sealed class ClaudeOptions
{
    public const string Section = "Claude";
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxRetryAttempts { get; set; } = 3;
}
