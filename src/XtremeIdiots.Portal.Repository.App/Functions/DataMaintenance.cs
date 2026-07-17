using System.Net;
using System.Net.Http.Headers;

using Azure.Core;
using Azure.Identity;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Portal.Repository.App.Services;

namespace XtremeIdiots.Portal.Repository.App.Functions;

public class DataMaintenance
{
    private readonly ILogger<DataMaintenance> _log;
    private readonly IRepositoryApiClient _repositoryApiClient;
    private readonly IConfiguration _configuration;
    private readonly IVpnDetectedTagReconciler _vpnDetectedTagReconciler;

    public DataMaintenance(
        ILogger<DataMaintenance> log,
        IRepositoryApiClient repositoryApiClient,
        IConfiguration configuration,
        IVpnDetectedTagReconciler vpnDetectedTagReconciler)
    {
        _log = log;
        _repositoryApiClient = repositoryApiClient;
        _configuration = configuration;
        _vpnDetectedTagReconciler = vpnDetectedTagReconciler;
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

    [Function(nameof(RunPrunePlayerIpAddressesHttp))]
    public async Task<HttpResponseData> RunPrunePlayerIpAddressesHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunPrunePlayerIpAddresses(null).ConfigureAwait(false);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(RunPrunePlayerIpAddresses))]
    public async Task RunPrunePlayerIpAddresses([TimerTrigger("0 30 2 * * *")] TimerInfo? myTimer)
    {
        _log.LogInformation("Pruning Player IP Addresses");
        await _repositoryApiClient.DataMaintenance.V1.PrunePlayerIpAddresses().ConfigureAwait(false);
        _log.LogInformation("Prune Player IP Addresses completed successfully");
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

    [Function(nameof(RunReconcileConnectedPlayerTagsHttp))]
    public async Task<HttpResponseData> RunReconcileConnectedPlayerTagsHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunReconcileConnectedPlayerTags(null).ConfigureAwait(false);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(RunReconcileConnectedPlayerTags))]
    public async Task RunReconcileConnectedPlayerTags([TimerTrigger("0 30 3 * * *")] TimerInfo? myTimer)
    {
        _log.LogInformation("Reconciling Connected Player Tags");
        await ReconcileConnectedPlayerTagsAsync(CancellationToken.None).ConfigureAwait(false);
        _log.LogInformation("Reconcile Connected Player Tags completed successfully");
    }

    [Function(nameof(RunReconcileVpnDetectedTagsHttp))]
    public async Task<HttpResponseData> RunReconcileVpnDetectedTagsHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunReconcileVpnDetectedTagsAsync(force: true, context.CancellationToken).ConfigureAwait(false);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(RunReconcileVpnDetectedTags))]
    public async Task RunReconcileVpnDetectedTags([TimerTrigger("0 0 4 * * *")] TimerInfo? myTimer, FunctionContext context)
    {
        await RunReconcileVpnDetectedTagsAsync(force: false, context.CancellationToken).ConfigureAwait(false);
    }

    private async Task RunReconcileVpnDetectedTagsAsync(bool force, CancellationToken cancellationToken)
    {
        _log.LogInformation("Reconciling VPN detected player tags");
        var summary = await _vpnDetectedTagReconciler.ReconcileAsync(force, cancellationToken).ConfigureAwait(false);
        _log.LogInformation(
            "VPN detected tag reconciliation completed. Candidates: {Candidates}; Players: {Players}; Added: {TagsAdded}; Removed: {TagsRemoved}; Skipped: {PlayersSkipped}",
            summary.Candidates,
            summary.PlayersEvaluated,
            summary.TagsAdded,
            summary.TagsRemoved,
            summary.PlayersSkipped);
    }

    private async Task ReconcileConnectedPlayerTagsAsync(CancellationToken cancellationToken)
    {
        var baseUrl = ResolveConfigurationValue("RepositoryApi:BaseUrl");
        var applicationAudience = ResolveConfigurationValue("RepositoryApi:ApplicationAudience");
        var managedIdentityClientId = ResolveConfigurationValue("AzureAppConfiguration:ManagedIdentityClientId");

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(applicationAudience))
        {
            _log.LogWarning(
                "Skipping connected-player tag reconciliation because required Repository API configuration values are missing.");
            return;
        }

        var reconcileUri = new Uri($"{baseUrl.TrimEnd('/')}/v1.0/data-maintenance/reconcile-connected-player-tags", UriKind.Absolute);
        var scope = BuildScope(applicationAudience);

        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = string.IsNullOrWhiteSpace(managedIdentityClientId)
                ? null
                : managedIdentityClientId
        });
        var token = await credential.GetTokenAsync(new TokenRequestContext([scope]), cancellationToken).ConfigureAwait(false);

        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, reconcileUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Connected-player tag reconciliation failed with status {(int)response.StatusCode}: {responseBody}");
        }
    }

    private static string BuildScope(string applicationAudience)
    {
        var trimmedAudience = applicationAudience.Trim();
        return trimmedAudience.EndsWith("/.default", StringComparison.OrdinalIgnoreCase)
            ? trimmedAudience
            : $"{trimmedAudience.TrimEnd('/')}/.default";
    }

    private string? ResolveConfigurationValue(string key)
    {
        return _configuration[key]
            ?? Environment.GetEnvironmentVariable(key.Replace(":", "__"));
    }
}