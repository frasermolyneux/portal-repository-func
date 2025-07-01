using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

using XtremeIdiots.Portal.RepositoryApiClient.V1;

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

    [Function(nameof(RunPruneChatMessagesHttp))]
    public async Task RunPruneChatMessagesHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunPruneChatMessages(null);
    }

    [Function(nameof(RunPruneChatMessages))]
    public async Task RunPruneChatMessages([TimerTrigger("0 0 * * * *")] TimerInfo? myTimer)
    {
        _log.LogInformation("Pruning Chat Messages");
        await _repositoryApiClient.DataMaintenance.V1.PruneChatMessages();
        _log.LogInformation("Prune Chat Messages completed successfully");
    }

    [Function(nameof(RunPruneGameServerEventsHttp))]
    public async Task RunPruneGameServerEventsHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunPruneGameServerEvents(null);
    }

    [Function(nameof(RunPruneGameServerEvents))]
    public async Task RunPruneGameServerEvents([TimerTrigger("0 0 1 * * *")] TimerInfo? myTimer)
    {
        _log.LogInformation("Pruning Game Server Events");
        await _repositoryApiClient.DataMaintenance.V1.PruneGameServerEvents();
        _log.LogInformation("Prune Game Server Events completed successfully");
    }

    [Function(nameof(RunPruneGameServerStatsHttp))]
    public async Task RunPruneGameServerStatsHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunPruneGameServerStats(null);
    }

    [Function(nameof(RunPruneGameServerStats))]
    public async Task RunPruneGameServerStats([TimerTrigger("0 0 2 * * *")] TimerInfo? myTimer)
    {
        _log.LogInformation("Pruning Game Server Stats");
        await _repositoryApiClient.DataMaintenance.V1.PruneGameServerStats();
        _log.LogInformation("Prune Game Server Stats completed successfully");
    }

    [Function(nameof(RunResetSystemAssignedPlayerTagsHttp))]
    public async Task RunResetSystemAssignedPlayerTagsHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunResetSystemAssignedPlayerTags(null);
    }

    [Function(nameof(RunResetSystemAssignedPlayerTags))]
    public async Task RunResetSystemAssignedPlayerTags([TimerTrigger("0 0 3 * * *")] TimerInfo? myTimer)
    {
        _log.LogInformation("Resetting System Assigned Player Tags");
        await _repositoryApiClient.DataMaintenance.V1.ResetSystemAssignedPlayerTags();
        _log.LogInformation("Reset System Assigned Player Tags completed successfully");
    }
}