# AI-Native PDLC Platform

A full-stack, production-scaffolded system that wraps every stage of the Product Development Lifecycle with Claude AI.

---

## Architecture

```
pdlc-platform/
│
├── backend/                        ← .NET 9 clean architecture
│   ├── Pdlc.Domain/                  Entities, enums, interfaces
│   ├── Pdlc.Application/             Stage services (1–4) + PR review
│   ├── Pdlc.Infrastructure/          ClaudeService, ADO clients, EF Core repos
│   ├── Pdlc.Api/                     ASP.NET Core controllers + Program.cs
│   └── Pdlc.Tests/                   xUnit test project
│
├── frontend/
│   └── pdlc-console/               ← Angular 19 standalone app (:4300)
│       └── src/app/features/
│           ├── stage1-requirements/  Analyse + refine + ADO sync
│           ├── stage2-design/        Component tree, API contract, data model
│           ├── stage3-codegen/       .NET + Angular boilerplate generation
│           ├── stage3b-review/       PR review trigger UI
│           └── stage4-testgen/       xUnit + Jasmine test stubs
│
├── ai-prompts/
│   └── v1/                         ← Versioned system prompts (loaded at runtime)
│       ├── stage1-requirements.md
│       ├── stage2-architecture.md
│       ├── stage3a-codegen.md
│       ├── stage3b-pr-review.md
│       └── stage4-testgen.md
│
├── pipelines/
│   ├── azure-pipelines.yml         ← Root pipeline (5 stages)
│   └── templates/
│       ├── build-lint.yml
│       ├── ai-code-review.yml
│       ├── test.yml
│       ├── ai-test-generation.yml
│       └── publish-artifacts.yml
│
└── ADO-SETUP.md                    ← Variable groups, PAT scopes, branch policies
```

---

## PDLC Stage Map

| Stage | Route | Backend | AI Operation |
|-------|-------|---------|--------------|
| S1 — Requirements | `/requirements` | `POST /api/pdlc/requirements/analyze` | Structured analysis: AC, edge cases, risks, effort |
| S1 — Refine | `/requirements/:id` | `POST /api/pdlc/requirements/:id/refine` | Multi-turn conversation |
| S1 — ADO Sync | UI button | `POST /api/pdlc/requirements/:id/sync-ado` | Creates User Story + child Tasks |
| S2 — Design | `/design/:id` | `POST /api/pdlc/design/generate` | Component tree + OpenAPI + data model |
| S3a — Code Gen | `/codegen/:id` | `POST /api/pdlc/codegen/generate` | .NET controller/service/repo + Angular component/service |
| S3b — PR Review | `/pr-review` | `POST /api/pdlc/pr-review/review` | Inline ADO PR comments (pipeline + manual) |
| S4 — Test Gen | `/testgen/:id` | `POST /api/pdlc/testgen/generate` | xUnit test class + Jasmine spec |

---

## Quick Start

### Prerequisites

- .NET 9 SDK
- Node.js 20 + Angular CLI
- PostgreSQL 14+ (create database `pdlc`)

### 1 — Backend

```bash
cd pdlc/backend/Pdlc.Api

# Set secrets (do not put in appsettings.json)
dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."
dotnet user-secrets set "Ado:PersonalAccessToken" "your-pat"

# Run (auto-migrates DB and starts on :5100)
dotnet run
```

### 2 — Frontend

```bash
cd pdlc/frontend/pdlc-console
npm install
npm start        # → http://localhost:4300
```

### 3 — CI/CD

See `ADO-SETUP.md` for:
- Variable group creation (pdlc-secrets, pdlc-config)
- ADO PAT scopes
- Branch protection rules
- Pipeline authorisation

---

## CI/CD Pipeline Stages

```
Push / PR created
      │
      ▼
① build-and-lint          Always — blocks downstream on failure
  .NET build + format     Hard gate
  Angular ESLint + Prettier

      │
      ├──────────────────────────────────┐
      ▼                                  ▼
② ai-code-review          (PR only)    ③ test                (always)
  Claude reviews diff                  xUnit + Karma
  Posts inline ADO comments            Publishes results + coverage
  Advisory — never blocks merge        Hard gate → blocks publish

      │
      ▼ (main only, post-merge)
④ ai-test-generation
  Generates xUnit + Jasmine stubs
  Opens draft ADO PR for human review

      │
      ▼ (main only)
⑤ publish-artifacts
  dotnet publish → pdlc-api artifact
  ng build --prod → pdlc-console artifact
```

---

## Claude Integration Details

All Claude calls go through `ClaudeService` (`Pdlc.Infrastructure/Claude/ClaudeService.cs`):

| Feature | Implementation |
|---------|---------------|
| Model | `claude-sonnet-4-6` (pinned) |
| Retry | Polly — 3 attempts, exponential backoff, 429 + 5xx |
| Circuit breaker | 5 failures → 30s open |
| Streaming | `IAsyncEnumerable<string>` via SSE |
| Multi-turn | `ContinueConversationAsync` with full history |
| Token logging | Every call persisted to `ClaudeUsageLogs` table |
| Prompt loading | Runtime file read from `/ai-prompts/v{version}/` |

---

## Branching Strategy

| Branch | Lifetime | Merge target | Required checks |
|--------|----------|-------------|-----------------|
| `main` | Permanent | — | Protected |
| `feature/pdlc-*` | ≤ 3 days | `main` via PR | build-and-lint ✅ + test ✅ + 1 reviewer |
| `fix/*` | ≤ 1 day | `main` via PR | build-and-lint ✅ + test ✅ |

Commit convention: `feat(pdlc-stage1): add requirement analysis endpoint`

---

## Extending prompts

Prompts are versioned in `/ai-prompts/`. To ship a new prompt version without redeploying:

1. Create `/ai-prompts/v2/stage1-requirements.md`
2. Update `prompt-registry.json` to set `"current": "v2"`
3. Pass `promptVersion: "v2"` in API requests (or update the default)

The `FilePromptRepository` reads from disk at runtime with an in-memory cache — no restart needed if you mount the prompts directory as a volume.
