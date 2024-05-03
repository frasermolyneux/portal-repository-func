using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

using XtremeIdiots.Portal.RepositoryApiClient;

namespace XtremeIdiots.Portal.RepositoryFunc;

public class DataMaintenance
{
    private readonly ILogger<DataMaintenance> _log;
    private readonly IRepositoryApiClient _repositoryApiClient;

    public DataMaintenance(
        ILogger<DataMaintenance> log,

        IRepositoryApiClient repositoryApiClient)
    {
        _log = log;
        _repositoryApiClient = repositoryApiClient;
    }

    [Function(nameof(RunDataMaintenanceHttp))]
    public async Task RunDataMaintenanceHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunDataMaintenance(null);
    }

    [Function(nameof(RunDataMaintenance))]
    public async Task RunDataMaintenance([TimerTrigger("0 0 * * * *")] TimerInfo myTimer)
    {
        _log.LogInformation("Performing Data Maintenance");

        await _repositoryApiClient.DataMaintenance.PruneChatMessages();
        await _repositoryApiClient.DataMaintenance.PruneGameServerEvents();
        await _repositoryApiClient.DataMaintenance.PruneGameServerStats();
    }
}