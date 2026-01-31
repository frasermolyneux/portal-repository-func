using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace XtremeIdiots.Portal.Repository.App.Functions
{
    public class HealthCheck
    {
        private readonly HealthCheckService healthCheck;

        public HealthCheck(HealthCheckService healthCheck)
        {
            this.healthCheck = healthCheck;
        }

        [Function(nameof(HealthCheck))]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req,
            FunctionContext context)
        {
            var healthStatus = await healthCheck.CheckHealthAsync().ConfigureAwait(false);
            return new OkObjectResult(Enum.GetName(typeof(HealthStatus), healthStatus.Status));
        }
    }
}
