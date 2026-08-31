# Portal Repository Functions

- This is a .NET 9 isolated Functions app for scheduled repository maintenance, map-popularity rebuilding, player-tag reconciliation, unclaimed-action reminders, and liveness/readiness endpoints.
- Repository operations use `XtremeIdiots.Portal.Repository.Api.Client.V1` with Entra ID authentication. GeoLocation integration is optional and limited to VPN-detection reconciliation.
- Preserve timer schedules, existing HTTP-trigger authorization, and shared execution paths between scheduled and manual invocations. Map popularity currently has no manual HTTP trigger.
- There is no app-wide explicit retry or idempotency framework. Keep operations safe under repeated timer/manual invocation and preserve per-recipient failure isolation in reminder processing.
- Azure App Configuration is optional and loads repository, GeoLocation, application, and Application Insights keys by environment label; Key Vault references use `DefaultAzureCredential`.
- Repository-client L1 caching uses library defaults. Current reads are advisory and mutations remain uncached; do not introduce read-then-write correctness that depends on fresh cached data.
- Terraform provisions the Function App, storage, role assignment, and health alerting using an azurerm backend and dev/prd inputs, while consuming platform and portal remote-state outputs. Preserve those interfaces.
