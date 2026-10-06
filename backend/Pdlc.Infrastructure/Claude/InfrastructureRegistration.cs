using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Pdlc.Domain.Interfaces;
using Pdlc.Infrastructure.Ado;
using Pdlc.Infrastructure.Persistence;
using Pdlc.Infrastructure.Persistence.Repositories;
using Pdlc.Infrastructure.Prompts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Pdlc.Infrastructure.Claude;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddPdlcInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Options ───────────────────────────────────────────────────────────
        services.Configure<ClaudeOptions>(configuration.GetSection(ClaudeOptions.Section));
        services.Configure<AdoOptions>(configuration.GetSection(AdoOptions.Section));
        services.Configure<PromptOptions>(configuration.GetSection(PromptOptions.Section));

        // ── Database ──────────────────────────────────────────────────────────
        services.AddDbContext<PdlcDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("PdlcDb")
                ?? "Host=localhost;Database=pdlc;Username=postgres;Password=postgres"));

        // ── Claude HttpClient + Polly retry ───────────────────────────────────
        services.AddHttpClient<IClaudeService, ClaudeService>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<ClaudeOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("x-api-key", opts.ApiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        })
        .AddPolicyHandler((sp, _) =>
        {
            var opts = sp.GetRequiredService<IOptions<ClaudeOptions>>().Value;
            return BuildRetryPolicy(opts.MaxRetryAttempts);
        })
        .AddPolicyHandler(BuildCircuitBreakerPolicy());

        // ── ADO services ──────────────────────────────────────────────────────
        services.AddHttpClient<IAdoBoardsService, AdoBoardsService>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<AdoOptions>>().Value;
            client.BaseAddress = new Uri($"https://dev.azure.com/{opts.Organisation}/");
            var pat = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($":{opts.PersonalAccessToken}"));
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {pat}");
        });

        services.AddHttpClient<IAdoReposService, AdoReposService>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<AdoOptions>>().Value;
            client.BaseAddress = new Uri($"https://dev.azure.com/{opts.Organisation}/");
            var pat = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($":{opts.PersonalAccessToken}"));
            client.DefaultRequestHeaders.Add("Authorization", $"Basic {pat}");
        });

        // add alongside the ADO HttpClient registration
        // services.AddHttpClient<IAdoReposService, GitHubReposService>((sp, client) =>
        // {
        //     var githubPat = configuration["GitHub:PersonalAccessToken"]!;
        //     client.DefaultRequestHeaders.Add("Authorization", $"Bearer {githubPat}");
        //     client.DefaultRequestHeaders.Add("User-Agent", "pdlc-ai-review");
        // });

        // ── Prompt repository ─────────────────────────────────────────────────
        services.AddSingleton<IPromptRepository, FilePromptRepository>();

        // ── Data repositories ─────────────────────────────────────────────────
        services.AddScoped<IRequirementRepository, RequirementRepository>();
        services.AddScoped<IDesignArtifactRepository, DesignArtifactRepository>();
        services.AddScoped<ICodeGenRepository, CodeGenRepository>();
        services.AddScoped<ITestGenRepository, TestGenRepository>();
        services.AddScoped<IPrReviewRepository, PrReviewRepository>();
        services.AddScoped<IUsageLogRepository, UsageLogRepository>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(int maxAttempts) =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                maxAttempts,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, attempt, _) =>
                {
                    Console.WriteLine(
                        $"[ClaudeRetry] Attempt {attempt} — waiting {timespan.TotalSeconds:F1}s — {outcome.Result?.StatusCode}");
                });

    private static IAsyncPolicy<HttpResponseMessage> BuildCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
}
