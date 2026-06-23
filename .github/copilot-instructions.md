# Copilot Instructions

> Shared conventions: see [`.github-copilot/.github/instructions/terraform.instructions.md`](../.github-copilot/.github/instructions/terraform.instructions.md) for the standard Terraform layout, providers, remote-state pattern, validation commands, and CI/CD workflows.
>
> <!-- Links use `../.github-copilot/` which resolves in the cloud-runner checkout (copilot-setup-steps.yml clones `.github-copilot` to the repo root). In local VS Code with the multi-root workspace, browse `../../.github-copilot/` instead. -->
>
> **Cloud agents (GitHub Copilot coding agent etc.):** read [`AGENTS.md`](../AGENTS.md) at the repo root first — it is the canonical brief that survives outside the local VS Code multi-root workspace.

## Scope

This Function App is intentionally small: it owns **scheduled maintenance** that needs to run away from the portal-web request path and the portal-server-agent / portal-server-events workers. Anything involving FTP, RCON, live stats, ban-file pushing, or log tailing now lives in [portal-server-agent](../../portal-server-agent/). Anything involving forum sync or map redirect lives in [portal-sync](../../portal-sync/). Real-time event ingest is in [portal-server-events](../../portal-server-events/).

If you're tempted to add an FTP/RCON/Service-Bus/GeoLocation dependency here, you're almost certainly in the wrong repo.

## Architecture

- .NET 9 isolated Azure Functions app. Host wiring in [src/XtremeIdiots.Portal.Repository.App/Program.cs](src/XtremeIdiots.Portal.Repository.App/Program.cs).
- Single dependency: the Portal Repository API via `XtremeIdiots.Portal.Repository.Api.Client.V1` (Entra ID auth).
- Optional Azure App Configuration load when `AzureAppConfiguration:Endpoint` is set, with environment-label selectors `RepositoryApi:*`, `XtremeIdiots.Portal.Repository.App:*` (prefix trimmed), and `ApplicationInsights:*`. Key Vault references resolved via `DefaultAzureCredential` with optional `ManagedIdentityClientId`.
- Observability via `MX.Observability.ApplicationInsights.WorkerService` (`AddObservability()`). Telemetry role name set in [TelemetryInitializer.cs](src/XtremeIdiots.Portal.Repository.App/TelemetryInitializer.cs). Sampling rules in [host.json](src/XtremeIdiots.Portal.Repository.App/host.json).

## Functions

All functions live under [src/XtremeIdiots.Portal.Repository.App/Functions/](src/XtremeIdiots.Portal.Repository.App/Functions/) and call repository API endpoints — they hold no business logic of their own.

- [DataMaintenance.cs](src/XtremeIdiots.Portal.Repository.App/Functions/DataMaintenance.cs) — four `TimerTrigger` cleanups (prune chat messages hourly, prune game-server events daily, prune game-server stats daily, reset system-assigned player tags daily). Each timer has a paired `AuthorizationLevel.Function` HTTP trigger for manual invocation.
- [MapPopularity.cs](src/XtremeIdiots.Portal.Repository.App/Functions/MapPopularity.cs) — hourly rebuild of map popularity rankings.
- [HealthCheck.cs](src/XtremeIdiots.Portal.Repository.App/Functions/HealthCheck.cs) — anonymous `GET /api/health/live` (process liveness) and `GET /api/health/ready` (aggregated `HealthCheckService` readiness status).

## Required configuration

| Setting | Notes |
|---|---|
| `RepositoryApi:BaseUrl` | Portal Repository API base URL |
| `RepositoryApi:ApplicationAudience` | Entra ID audience for the repository API |
| `AzureAppConfiguration:Endpoint` | Optional — enables App Config + Key Vault load |
| `AzureAppConfiguration:ManagedIdentityClientId` | Optional — user-assigned MI client ID for App Config / KV |
| `AzureAppConfiguration:Environment` | App Config label (`dev` / `prd`) |

Local secrets via user secrets (see `UserSecretsId` in the csproj). `local.settings.json` is excluded from publish.

## Local development

```pwsh
cd src/XtremeIdiots.Portal.Repository.App
dotnet clean
dotnet build
dotnet test ../ --filter "FullyQualifiedName!~IntegrationTests"
```

Application Insights is optional for local runs.

## Patterns

- New timers: mirror existing `TimerTrigger` cron expressions and add a paired HTTP trigger for manual execution. Always log a start + completion message.
- All repository calls use `.ConfigureAwait(false)`.
- Don't introduce new external dependencies (FTP, RCON, Service Bus, GeoLocation, etc.) — those belong in [portal-server-agent](../../portal-server-agent/), [portal-server-events](../../portal-server-events/), or [portal-sync](../../portal-sync/).

## CI/CD

See [docs/development-workflows.md](docs/development-workflows.md). Main jobs: `build-and-test`, `pr-verify`, `deploy-dev`, `deploy-prd` with Terraform plan/apply gates. Copilot branches skip plans unless labelled.
