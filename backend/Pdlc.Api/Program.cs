using Microsoft.EntityFrameworkCore;
using Pdlc.Application.CodeGen;
using Pdlc.Application.Design;
using Pdlc.Application.PrReview;
using Pdlc.Application.Requirements;
using Pdlc.Application.TestGen;
using Pdlc.Infrastructure.Claude;
using Pdlc.Infrastructure.Persistence;
using Serilog;
using Serilog.Events;

// ── Bootstrap Serilog ─────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/pdlc-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ── Infrastructure (Claude, ADO, EF Core, Repos) ──────────────────────────
    builder.Services.AddPdlcInfrastructure(builder.Configuration);

    // ── Application Services (one per PDLC stage) ─────────────────────────────
    builder.Services.AddScoped<IRequirementAnalysisService, RequirementAnalysisService>();
    builder.Services.AddScoped<IDesignAssistService, DesignAssistService>();
    builder.Services.AddScoped<ICodeGenerationService, CodeGenerationService>();
    builder.Services.AddScoped<IPrReviewService, PrReviewService>();
    builder.Services.AddScoped<ITestGenerationService, TestGenerationService>();

    // ── API ───────────────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();

    // ── CORS (Angular dev server) ─────────────────────────────────────────────
    builder.Services.AddCors(opts => opts.AddDefaultPolicy(p =>
        p.WithOrigins("http://localhost:4300")
         .AllowAnyHeader()
         .AllowAnyMethod()));

    // ── Health checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<PdlcDbContext>("postgres");

    var app = builder.Build();

    // ── Migrate DB on startup ─────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PdlcDbContext>();
        try { await db.Database.MigrateAsync(); }
        catch { await db.Database.EnsureCreatedAsync(); }
        Log.Information("PDLC database ready");
    }

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.UseCors();
    app.UseHttpsRedirection();
    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("PDLC API starting on {Urls}", builder.Configuration["Urls"] ?? "http://localhost:5100");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PDLC API failed to start");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
