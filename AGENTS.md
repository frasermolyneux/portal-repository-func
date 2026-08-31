# AGENTS.md — portal-repository-func

This repository is a .NET 10 isolated Azure Functions workload for scheduled Portal Repository maintenance, reconciliation, reminders, and health checks.

## Layout

- `src/XtremeIdiots.Portal.Repository.App` — function host, triggers, client composition, and operational services.
- `src/XtremeIdiots.Portal.Repository.App.Tests` — function, service, health, and startup-composition tests.
- `src/XtremeIdiots.Portal.Repository.App.slnx` — solution.
- `terraform` — Function App, storage, identity assignment, monitoring, and remote-state consumption.

The exact SDK is pinned in `global.json`. The app consumes the V1 Portal Repository typed client and optionally the GeoLocation client for VPN-tag reconciliation.

## Useful commands

```pwsh
dotnet build src\XtremeIdiots.Portal.Repository.App.slnx
dotnet test src\XtremeIdiots.Portal.Repository.App.slnx
dotnet format src\XtremeIdiots.Portal.Repository.App.slnx --verify-no-changes
terraform -chdir=terraform fmt -check -recursive
```

Run Terraform init/validate/plan only for infrastructure work, with matching `terraform\backends\<env>.backend.hcl` and `terraform\tfvars\<env>.tfvars`.

## Repository boundaries

- Keep orchestration in the function workload and domain persistence/behavior behind repository API operations.
- Preserve established schedules and trigger authorization. Maintenance, reconciliation, and reminder operations with HTTP entry points use function-key authorization; health endpoints remain anonymous. Map-popularity rebuild is timer-only.
- Timer and manual paths for the same operation must share behavior. Consider duplicate invocation and partial failure before adding side effects; there is no repository-wide app-level retry or idempotency framework.
- Preserve V1 repository-client request/response compatibility and startup composition. Keep async API calls cancellation-aware where supported and consistent with existing `ConfigureAwait(false)` usage.
- VPN-tag reconciliation uses bounded paging/batching, skips incomplete intelligence, and changes a tag only when detected state differs. Preserve these operational safeguards.
- Unclaimed-action reminders intentionally continue after an individual notification failure and warn when query page limits are reached.
- Configuration comes from environment/user secrets and optional Azure App Configuration with Key Vault resolution; do not embed credentials.
- Preserve the AzureRM provider constraint, azurerm backend, dev/prd environment pairing, and remote-state interfaces to platform workloads/monitoring and portal foundations.

## Authoritative details

- `src/XtremeIdiots.Portal.Repository.App\Program.cs`
- `src/XtremeIdiots.Portal.Repository.App\Functions`
- `src/XtremeIdiots.Portal.Repository.App\Services`
- `docs/development-workflows.md`
