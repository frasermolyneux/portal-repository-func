using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.GameServers;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.Maps;
using XtremeIdiots.Portal.RepositoryApiClient.V1;
using XtremeIdiots.Portal.ServersApiClient;

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
            GameType[] gameTypes = new GameType[] { GameType.CallOfDuty2, GameType.CallOfDuty4, GameType.CallOfDuty5, GameType.Insurgency };
            var gameServersApiResponse = await repositoryApiClient.GameServers.V1.GetGameServers(gameTypes, null, GameServerFilter.LiveTrackingEnabled, 0, 50, null);

            if (!gameServersApiResponse.IsSuccess || gameServersApiResponse.Result == null)
            {
                logger.LogCritical("Failed to retrieve game servers from repository");
                return;
            }

            List<CreateGameServerStatDto> gameServerStatDtos = new List<CreateGameServerStatDto>();

            foreach (var gameServerDto in gameServersApiResponse.Result.Entries)
            {
                using (logger.BeginScope(gameServerDto.TelemetryProperties))
                {
                    if (string.IsNullOrWhiteSpace(gameServerDto.Hostname) || gameServerDto.QueryPort == 0)
                        continue;

                    if (!string.IsNullOrWhiteSpace(gameServerDto.RconPassword))
                    {
                        var serverQueryApiResponse = await serversApiClient.Query.GetServerStatus(gameServerDto.GameServerId);

                        if (!serverQueryApiResponse.IsSuccess || serverQueryApiResponse.Result == null)
                        {
                            logger.LogWarning($"Failed to retrieve server query result for game server {gameServerDto.GameServerId}");
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(serverQueryApiResponse.Result.Map))
                        {
                            if (!memoryCache.TryGetValue($"{gameServerDto.GameType}-{serverQueryApiResponse.Result.Map}", out bool mapExists))
                            {
                                var mapDto = await repositoryApiClient.Maps.V1.GetMap(gameServerDto.GameType, serverQueryApiResponse.Result.Map);

                                if (mapDto.IsNotFound)
                                    await repositoryApiClient.Maps.V1.CreateMap(new CreateMapDto(gameServerDto.GameType, serverQueryApiResponse.Result.Map));

                                memoryCache.Set($"{gameServerDto.GameType}-{serverQueryApiResponse.Result.Map}", true);
                            }

                            gameServerStatDtos.Add(new CreateGameServerStatDto(gameServerDto.GameServerId, serverQueryApiResponse.Result.PlayerCount, serverQueryApiResponse.Result.Map));
                        }
                    }
                }
            }

            if (gameServerStatDtos.Any())
                await repositoryApiClient.GameServersStats.V1.CreateGameServerStats(gameServerStatDtos);
        }
    }
}
