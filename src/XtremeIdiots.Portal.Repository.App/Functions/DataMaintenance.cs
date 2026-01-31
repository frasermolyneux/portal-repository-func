using System.Net;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Repository.App.Functions;

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
    public async Task<HttpResponseData> RunPruneChatMessagesHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunPruneChatMessages(null).ConfigureAwait(false);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(RunPruneChatMessages))]
    public async Task RunPruneChatMessages([TimerTrigger("0 0 * * * *")] TimerInfo? myTimer)
    {
        _log.LogInformation("Pruning Chat Messages");
        await _repositoryApiClient.DataMaintenance.V1.PruneChatMessages().ConfigureAwait(false);
        _log.LogInformation("Prune Chat Messages completed successfully");
    }

    [Function(nameof(RunPruneGameServerEventsHttp))]
    public async Task<HttpResponseData> RunPruneGameServerEventsHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunPruneGameServerEvents(null).ConfigureAwait(false);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(RunPruneGameServerEvents))]
    public async Task RunPruneGameServerEvents([TimerTrigger("0 0 1 * * *")] TimerInfo? myTimer)
    {
        _log.LogInformation("Pruning Game Server Events");
        await _repositoryApiClient.DataMaintenance.V1.PruneGameServerEvents().ConfigureAwait(false);
        _log.LogInformation("Prune Game Server Events completed successfully");
    }

    [Function(nameof(RunPruneGameServerStatsHttp))]
    public async Task<HttpResponseData> RunPruneGameServerStatsHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunPruneGameServerStats(null).ConfigureAwait(false);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(RunPruneGameServerStats))]
    public async Task RunPruneGameServerStats([TimerTrigger("0 0 2 * * *")] TimerInfo? myTimer)
    {
        _log.LogInformation("Pruning Game Server Stats");
        await _repositoryApiClient.DataMaintenance.V1.PruneGameServerStats().ConfigureAwait(false);
        _log.LogInformation("Prune Game Server Stats completed successfully");
    }

    [Function(nameof(RunResetSystemAssignedPlayerTagsHttp))]
    public async Task<HttpResponseData> RunResetSystemAssignedPlayerTagsHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunResetSystemAssignedPlayerTags(null).ConfigureAwait(false);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(RunResetSystemAssignedPlayerTags))]
    public async Task RunResetSystemAssignedPlayerTags([TimerTrigger("0 0 3 * * *")] TimerInfo? myTimer)
    {
        _log.LogInformation("Resetting System Assigned Player Tags");
        await _repositoryApiClient.DataMaintenance.V1.ResetSystemAssignedPlayerTags().ConfigureAwait(false);
        _log.LogInformation("Reset System Assigned Player Tags completed successfully");
    }
}