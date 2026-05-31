# AGENTS.md — portal-repository-func

.NET 9 isolated Azure Functions app hosting **scheduled maintenance** for the Portal Repository (timer-triggered cleanups + map-popularity rebuild + a health endpoint). Single external dependency: the Portal Repository API via the typed client. Anything FTP / RCON / Service Bus / GeoLocation / forum-sync belongs in **other** repos — this one is intentionally small.

This file is the brief for the **GitHub Copilot coding agent** (and any other agent that follows the [agents.md](https://agents.md) convention) when it runs in a cloud runner without the local VS Code multi-root workspace context.

> If you are a human reading this in VS Code, prefer `.github/copilot-instructions.md` for project orientation. `AGENTS.md` is the agent execution brief.

---

## Required reading (read these BEFORE doing any work)

The `copilot-setup-steps.yml` workflow checks out `frasermolyneux/.github-copilot` at `./.github-copilot/` in the runner, so the paths below resolve.

1. `.github/copilot-instructions.md` — repo-specific orientation, build commands, conventions, **scope rules**
2. `.github-copilot/.github/instructions/personal.working-preferences.instructions.md`
3. `.github-copilot/.github/copilot-instructions.md` — org-wide catalog
4. Stack-specific files — see **Stack guardrails** below

---

## Stack guardrails

### Tenant facts (always-on)
- `tenant.subscriptions`, `tenant.regions`, `tenant.identity`

### Enforceable standards
- `standards.oidc-and-secrets` — **no client secrets**
- `standards.dotnet-project`
- `standards.azure-naming`, `standards.azure-tagging`, `standards.terraform-style`
- `standards.branching-and-prs`

### Patterns
- `patterns.api-client` — consumes the Portal Repository V1 client
- `patterns.nbgv-versioning`
- `patterns.terraform-remote-state`

### Platform consumption contracts
- `platform.workloads`, `platform.monitoring`, `platform.hosting`

### Shared
- `shared.api-client-abstractions`
- `shared.observability-appinsights`

---

## Build, test, format

```pwsh
cd src/XtremeIdiots.Portal.Repository.App
dotnet clean
dotnet build
cd ../..
dotnet test src --filter "FullyQualifiedName!~IntegrationTests"
dotnet format src --verify-no-changes

terraform -chdir=terraform fmt -check -recursive
terraform -chdir=terraform init -backend-config=backends/dev.backend.hcl
terraform -chdir=terraform validate
terraform -chdir=terraform plan -var-file=tfvars/dev.tfvars
```

---

## Do NOT

- ❌ Do not `git commit`, `git push`, force-push, rebase, or branch-mutate. Work on the assigned branch only.
- ❌ Do not introduce client secrets. OIDC + managed identity (with optional user-assigned client ID for App Config / Key Vault).
- ❌ **Do not add FTP, RCON, Service Bus, GeoLocation, or forum dependencies here** — wrong repo:
  - FTP / RCON / live stats / ban-file push / log tailing → [`portal-server-agent`](../portal-server-agent/)
  - Service Bus event consumption → [`portal-server-events`](../portal-server-events/)
  - Forum sync / map redirect → [`portal-sync`](../portal-sync/)
- ❌ Do not bypass `dotnet format`, `dotnet test`, `terraform fmt`, or `terraform validate`.
- ❌ Do not add a timer trigger without a paired `AuthorizationLevel.Function` HTTP trigger for manual execution.
- ❌ Do not modify `.github/workflows/`, `.github/dependabot.yml`, or `version.json` unless that is the explicit task.
- ❌ Do not call without `.ConfigureAwait(false)` on async repository API calls.

---

## Opening the PR

You MUST use `.github/PULL_REQUEST_TEMPLATE.md` as your PR body — do **not** write a freeform body. The org template is inherited from `frasermolyneux/.github` and GitHub pre-populates it when you open the PR. Concretely:

1. Fill `## Summary` (one line) and `Closes #<issue>`.
2. Tick the relevant `## Type of change` box.
3. Paste the **actual command output** from your Build, Tests, and Format check runs into `## Validation evidence`. Show the real summary line, not "tests passed".
4. Fill `## Risk and rollout` — blast radius, auto-deploy?, manual steps post-merge, rollback plan.
5. Tick **every** box in `## Agent attestation`.
6. Delete `## Consumer impact` only if no published contract (Abstractions / Client NuGet / Service Bus DTO / Terraform output) changed.

Complete the `## Agent attestation` section before requesting review; reviewers use it as a readiness checklist.

---

## Pre-PR checks (run before you open the PR)

- [ ] `dotnet build` succeeds (clean)
- [ ] `dotnet test --filter "FullyQualifiedName!~IntegrationTests"` passes
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `terraform fmt -check -recursive` passes
- [ ] `terraform validate` + `terraform plan -var-file=tfvars/dev.tfvars` succeed
- [ ] Each new timer has a paired HTTP trigger
- [ ] No new external dependencies (FTP / RCON / Service Bus / GeoLocation / forums)
- [ ] No new secrets / GUIDs / connection strings
- [ ] PR body cites each acceptance criterion
- [ ] Risk/rollout section filled in

---

## Escalation

If you hit any of the conditions below, **open the PR as draft** and **apply the `needs-decision` label** instead of pushing forward to ready-for-review. Post a comment on the originating issue summarising what's blocking you and what decision is needed.

Stop and escalate when:

- The task implies adding FTP / RCON / Service Bus / GeoLocation / forum logic here (wrong repo — request a re-target).
- A `code-review` finding is **High** and cannot be resolved in-scope.
- Required App Configuration keys are missing in the dev environment.
