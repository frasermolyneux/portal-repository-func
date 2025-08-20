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
            GameType[] gameTypes = new GameType[] { GameType.CallOfDuty2, GameType.CallOfDuty4, GameType.CallOfDuty5, GameType.Insurgency };
            var gameServersApiResponse = await repositoryApiClient.GameServers.V1.GetGameServers(gameTypes, null, GameServerFilter.LiveTrackingEnabled, 0, 50, null);

            if (!gameServersApiResponse.IsSuccess || gameServersApiResponse.Result == null)
            {
                logger.LogCritical("Failed to retrieve game servers from repository");
                return;
            }

            List<CreateGameServerStatDto> gameServerStatDtos = new List<CreateGameServerStatDto>();

            foreach (var gameServerDto in gameServersApiResponse.Result.Data?.Items ?? Enumerable.Empty<GameServerDto>())
            {
                using (logger.BeginScope(gameServerDto.TelemetryProperties))
                {
                    if (string.IsNullOrWhiteSpace(gameServerDto.Hostname) || gameServerDto.QueryPort == 0)
                        continue;

                    if (!string.IsNullOrWhiteSpace(gameServerDto.RconPassword))
                    {
                        var getServerStatusResult = await serversApiClient.Query.V1.GetServerStatus(gameServerDto.GameServerId);

                        if (!getServerStatusResult.IsSuccess || getServerStatusResult.Result?.Data == null)
                        {
                            logger.LogWarning($"Failed to retrieve server query result for game server {gameServerDto.GameServerId}");
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(getServerStatusResult.Result.Data.Map))
                        {
                            if (!memoryCache.TryGetValue($"{gameServerDto.GameType}-{getServerStatusResult.Result.Data.Map}", out bool mapExists))
                            {
                                var mapDto = await repositoryApiClient.Maps.V1.GetMap(gameServerDto.GameType, getServerStatusResult.Result.Data.Map);

                                if (mapDto.IsNotFound)
                                    await repositoryApiClient.Maps.V1.CreateMap(new CreateMapDto(gameServerDto.GameType, getServerStatusResult.Result.Data.Map));

                                memoryCache.Set($"{gameServerDto.GameType}-{getServerStatusResult.Result.Data.Map}", true);
                            }

                            gameServerStatDtos.Add(new CreateGameServerStatDto(gameServerDto.GameServerId, getServerStatusResult.Result.Data.PlayerCount, getServerStatusResult.Result.Data.Map));
                        }
                    }
                }
            }

            if (gameServerStatDtos.Any())
                await repositoryApiClient.GameServersStats.V1.CreateGameServerStats(gameServerStatDtos);
        }
    }
}
