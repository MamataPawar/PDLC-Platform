# ADO Variable Group Setup Guide
# ================================
# Before running the pipeline, create two variable groups in
# Azure DevOps → Pipelines → Library, then link them in the pipeline.

## Group 1: `pdlc-secrets`

| Variable Name    | Value                              | Secret? |
|------------------|------------------------------------|---------|
| CLAUDE_API_KEY   | Your Anthropic API key (sk-ant-…)  | ✅ Yes  |
| ADO_PAT          | ADO Personal Access Token          | ✅ Yes  |

### How to create the ADO PAT

1. Go to Azure DevOps → User settings (top-right avatar) → Personal Access Tokens
2. Click **+ New Token**
3. Set the following scopes:
   - **Code**: Read & Write  ← needed for diff fetching and committing test stubs
   - **Work Items**: Read & Write  ← needed for creating User Stories and Tasks
   - **Pull Request Threads**: Read & Write  ← needed for posting review comments
4. Set expiry (recommend 90 days, rotate regularly)
5. Copy the token immediately — it is only shown once
6. Paste it as `ADO_PAT` in the `pdlc-secrets` variable group, marked as secret

---

## Group 2: `pdlc-config`

| Variable Name | Value                            | Secret? |
|---------------|----------------------------------|---------|
| PDLC_API_URL  | http://your-pdlc-api-host:5100   | No      |
| ADO_ORG       | Your ADO organisation name       | No      |
| ADO_PROJECT   | Your ADO project name            | No      |
| ADO_REPO      | Your repository name             | No      |

> **Local development**: PDLC_API_URL = `http://localhost:5100`
> **Hosted agent**: PDLC_API_URL = the URL of your deployed PDLC API

---

## Linking variable groups to the pipeline

In `azure-pipelines.yml` the groups are already referenced:

```yaml
variables:
  - group: pdlc-secrets
  - group: pdlc-config
```

To authorise the pipeline to use them:
1. Go to **Pipelines → Library → pdlc-secrets → Pipeline permissions**
2. Click the **+** button and select your pipeline
3. Repeat for `pdlc-config`

---

## Branch policies (Main branch protection)

In **Repos → Branches → main → Branch policies**, configure:

```
✅ Require minimum reviewers: 1
   ☐ Allow requestors to approve their own changes

✅ Check for linked work items: Required
   (PDLC Stage 1 automatically creates work items — link them)

✅ Check for comment resolution: Required

✅ Limit merge types:
   ✅ Squash merge    (keeps main history clean)
   ☐ Rebase
   ☐ Basic merge

✅ Build validation:
   Pipeline: azure-pipelines.yml
   Trigger: Automatic
   Policy: Required
   Build expiration: Immediately

✅ Status checks (add after first pipeline run):
   - pdlc-platform/build-and-lint   → Required
   - pdlc-platform/test             → Required
   - pdlc-platform/ai-code-review   → Optional (advisory)

✅ Automatically include code reviewers:
   Add your team here — reviewers auto-added on every PR
```

---

## PR Title policy (Conventional Commits enforcement)

In **Repos → Branches → main → Branch policies → Require a merge strategy**,
add a **Status check** or use a service hook.

For PR title validation, add a build step in `build-lint.yml`:

```yaml
- script: |
    PR_TITLE="$(Build.SourceVersionMessage)"
    PATTERN="^(feat|fix|chore|refactor|test|docs|ci)(\(.+\))?: .{10,72}$"
    if ! echo "$PR_TITLE" | grep -qP "$PATTERN"; then
      echo "##vso[task.logissue type=error]PR title does not follow Conventional Commits format."
      echo "Expected: feat(scope): description"
      exit 1
    fi
  displayName: 'Validate PR title (Conventional Commits)'
  condition: eq(variables['Build.Reason'], 'PullRequest')
```

---

## User secrets for local development

Set these instead of putting secrets in appsettings.json:

```bash
cd pdlc/backend/Pdlc.Api

dotnet user-secrets set "Claude:ApiKey" "sk-ant-your-key-here"
dotnet user-secrets set "Ado:PersonalAccessToken" "your-ado-pat-here"
dotnet user-secrets set "ConnectionStrings:PdlcDb" "Host=localhost;Database=pdlc;Username=postgres;Password=postgres"
```

Or use environment variables:

```bash
export Claude__ApiKey="sk-ant-your-key-here"
export Ado__PersonalAccessToken="your-ado-pat-here"
```

---

## Database setup

```sql
-- Run as postgres superuser once
CREATE DATABASE pdlc;
```

EF Core migrations run automatically on first API startup via `MigrateAsync()`.

To generate migrations manually after domain changes:

```bash
cd pdlc/backend/Pdlc.Api
dotnet ef migrations add <MigrationName> \
  --project ../Pdlc.Infrastructure \
  --startup-project . \
  --context PdlcDbContext \
  --output-dir ../Pdlc.Infrastructure/Persistence/Migrations
```
