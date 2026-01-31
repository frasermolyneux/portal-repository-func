using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Repository.App.Functions;

public class MapPopularity
{
    private readonly ILogger<MapPopularity> _log;
    private readonly IRepositoryApiClient _repositoryApiClient;

    public MapPopularity(
        ILogger<MapPopularity> log,
        IRepositoryApiClient repositoryApiClient)
    {
        _log = log;
        _repositoryApiClient = repositoryApiClient;
    }

    [Function(nameof(RunRebuildMapPopularity))]
    public async Task RunRebuildMapPopularity([TimerTrigger("0 0 */1 * * *")] TimerInfo myTimer)
    {
        _log.LogInformation("Performing Rebuild of Map Popularity");

        await _repositoryApiClient.Maps.V1.RebuildMapPopularity().ConfigureAwait(false);
    }
}