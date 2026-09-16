# Function telemetry

The application has two Application Insights telemetry pipelines:

- The Azure Functions host emits invocation lifecycle traces, request records,
  exceptions, and runtime warnings.
- The .NET isolated worker sends application logs and custom telemetry directly
  to Application Insights from `Program.cs`.

The worker `TelemetryFilterProcessor` does not receive host-originated telemetry.
Host volume is therefore controlled independently in `host.json` with deterministic
category filters:

```json
"logLevel": {
  "Function": "Warning",
  "Host.Results": "Error",
  "Function.HealthCheck": "Warning"
}
```

`Function = Warning` suppresses successful function start/completion traces, which
the Functions host emits at `Information`, while retaining warning, error, and
exception traces.

`Host.Results = Error` suppresses successful invocation request records while
retaining failed function execution records in Application Insights. The existing
Terraform monitoring uses Azure Resource Health activity logs rather than successful
function request records, so its alert semantics are unchanged.

Host sampling remains disabled. Failures are retained by severity rather than by
probabilistic sampling. The isolated worker pipeline, telemetry filtering, audit
behavior, function processing, retries, and failure handling are unchanged.

References:

- [Configure Azure Functions monitoring categories and log levels](https://learn.microsoft.com/azure/azure-functions/configure-monitoring#configure-categories)
- [Configure Azure Functions host settings](https://learn.microsoft.com/azure/azure-functions/functions-host-json#applicationinsights)
- [.NET isolated worker Application Insights](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide#application-insights)
