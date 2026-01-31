using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Maps;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Repository.App.Functions
{
    public class SnapshotGameServerStats
    {
        private readonly ILogger<SnapshotGameServerStats> logger;
        private readonly IRepositoryApiClient repositoryApiClient;
        private readonly IServersApiClient serversApiClient;
        private readonly IMemoryCache memoryCache;

        public SnapshotGameServerStats(
            ILogger<SnapshotGameServerStats> logger,
            IRepositoryApiClient repositoryApiClient,
            IServersApiClient serversApiClient,
            IMemoryCache memoryCache)
        {
            this.logger = logger;
            this.repositoryApiClient = repositoryApiClient;
            this.serversApiClient = serversApiClient;
            this.memoryCache = memoryCache;
        }

        [Function(nameof(RunSnapshotGameServerStats))]
        public async Task RunSnapshotGameServerStats([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
        {
            GameType[] gameTypes = [GameType.CallOfDuty2, GameType.CallOfDuty4, GameType.CallOfDuty5, GameType.Insurgency];
            var gameServersApiResponse = await repositoryApiClient.GameServers.V1.GetGameServers(gameTypes, null, GameServerFilter.LiveTrackingEnabled, 0, 50, null).ConfigureAwait(false);

            if (!gameServersApiResponse.IsSuccess || gameServersApiResponse.Result == null)
            {
                logger.LogCritical("Failed to retrieve game servers from repository");
                return;
            }

            List<CreateGameServerStatDto> gameServerStatDtos = [];

            foreach (var gameServerDto in gameServersApiResponse.Result.Data?.Items ?? Enumerable.Empty<GameServerDto>())
            {
                using (logger.BeginScope(gameServerDto.TelemetryProperties))
                {
                    if (string.IsNullOrWhiteSpace(gameServerDto.Hostname) || gameServerDto.QueryPort == 0)
                        continue;

                    if (!string.IsNullOrWhiteSpace(gameServerDto.RconPassword))
                    {
                        var getServerStatusResult = await serversApiClient.Query.V1.GetServerStatus(gameServerDto.GameServerId).ConfigureAwait(false);

                        if (!getServerStatusResult.IsSuccess || getServerStatusResult.Result?.Data == null)
                        {
                            logger.LogWarning($"Failed to retrieve server query result for game server {gameServerDto.GameServerId}");
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(getServerStatusResult.Result.Data.Map))
                        {
                            await CreateMapIfNotExists(gameServerDto, getServerStatusResult.Result.Data.Map).ConfigureAwait(false);

                            gameServerStatDtos.Add(new CreateGameServerStatDto(gameServerDto.GameServerId, getServerStatusResult.Result.Data.PlayerCount, getServerStatusResult.Result.Data.Map));
                        }
                    }
                }
            }

            if (gameServerStatDtos.Any())
                await repositoryApiClient.GameServersStats.V1.CreateGameServerStats(gameServerStatDtos).ConfigureAwait(false);
        }

        private async Task CreateMapIfNotExists(GameServerDto gameServerDto, string mapName)
        {
            if (!memoryCache.TryGetValue($"{gameServerDto.GameType}-{mapName}", out bool mapExists))
            {
                var getMapApiResult = await repositoryApiClient.Maps.V1.GetMap(gameServerDto.GameType, mapName).ConfigureAwait(false);

                if (getMapApiResult.IsNotFound)
                {
                    var createMapApiResult = await repositoryApiClient.Maps.V1.CreateMap(new CreateMapDto(gameServerDto.GameType, mapName)).ConfigureAwait(false);
                    if (createMapApiResult.IsSuccess)
                    {
                        memoryCache.Set($"{gameServerDto.GameType}-{mapName}", true);
                    }
                    else if (createMapApiResult.IsConflict)
                    {
                        logger.LogWarning($"Map {mapName} already exists for game type {gameServerDto.GameType}. Caching the map existence.");
                        memoryCache.Set($"{gameServerDto.GameType}-{mapName}", true);
                    }
                    else
                    {
                        logger.LogError($"Failed to create map {mapName} for game type {gameServerDto.GameType}");
                    }
                }
            }
        }
    }
}
