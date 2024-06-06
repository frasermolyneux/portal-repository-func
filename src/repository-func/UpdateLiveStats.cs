
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using MX.GeoLocation.GeoLocationApi.Client;

using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.GameServers;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.Players;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.RecentPlayers;
using XtremeIdiots.Portal.RepositoryApiClient;
using XtremeIdiots.Portal.RepositoryFunc.Extensions;
using XtremeIdiots.Portal.ServersApiClient;

namespace XtremeIdiots.Portal.RepositoryFunc
{
    public class UpdateLiveStats
    {
        private readonly ILogger<UpdateLiveStats> logger;
        private readonly IRepositoryApiClient repositoryApiClient;
        private readonly IServersApiClient serversApiClient;
        private readonly IGeoLocationApiClient geoLocationClient;
        private readonly TelemetryClient telemetryClient;
        private readonly IMemoryCache memoryCache;

        public UpdateLiveStats(
            ILogger<UpdateLiveStats> logger,
            IRepositoryApiClient repositoryApiClient,
            IServersApiClient serversApiClient,
            IGeoLocationApiClient geoLocationClient,
            TelemetryClient telemetryClient,
            IMemoryCache memoryCache)
        {
            this.logger = logger;
            this.repositoryApiClient = repositoryApiClient;
            this.serversApiClient = serversApiClient;
            this.geoLocationClient = geoLocationClient;
            this.telemetryClient = telemetryClient;
            this.memoryCache = memoryCache;
        }


        [Function(nameof(RunUpdateLiveStats))]
        public async Task RunUpdateLiveStats([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
        {
            var gameTypes = new GameType[] { GameType.CallOfDuty2, GameType.CallOfDuty4, GameType.CallOfDuty5, GameType.Insurgency };
            var gameServersApiResponse = await repositoryApiClient.GameServers.GetGameServers(gameTypes, null, GameServerFilter.LiveTrackingEnabled, 0, 50, null);

            if (!gameServersApiResponse.IsSuccess || gameServersApiResponse.Result == null)
            {
                logger.LogCritical("Failed to retrieve game servers from repository");
                return;
            }

            foreach (var gameServerDto in gameServersApiResponse.Result.Entries)
            {
                if (string.IsNullOrWhiteSpace(gameServerDto.Hostname) || gameServerDto.QueryPort == 0)
                    continue;

                var livePlayerDtos = new List<CreateLivePlayerDto>();

                try
                {
                    if (!string.IsNullOrWhiteSpace(gameServerDto.RconPassword))
                    {
                        livePlayerDtos = await UpdateLivePlayersFromRcon(gameServerDto);
                        livePlayerDtos = await UpdateLivePlayersFromQuery(gameServerDto, livePlayerDtos);
                        livePlayerDtos = await EnrichPlayersWithGeoLocation(livePlayerDtos);

                        await UpdateRecentPlayersWithLivePlayers(livePlayerDtos);
                    }
                    else
                    {
                        livePlayerDtos = await UpdateLivePlayersFromQuery(gameServerDto, livePlayerDtos);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to update live stats for server '{gameServerDto.GameServerId}'");
                    continue;
                }

                MetricTelemetry telemetry = new()
                {
                    Name = "PlayerCount",
                    Sum = livePlayerDtos.Count
                };

                telemetry.Properties.Add("GameServerId", gameServerDto.GameServerId.ToString());
                telemetry.Properties.Add("GameServerName", gameServerDto.Title);

                telemetryClient.TrackMetric(telemetry);

                await repositoryApiClient.LivePlayers.SetLivePlayersForGameServer(gameServerDto.GameServerId, livePlayerDtos);
            }
        }

        private async Task<List<CreateLivePlayerDto>> UpdateLivePlayersFromRcon(GameServerDto gameServerDto)
        {
            var rconQueryApiResponse = await serversApiClient.Rcon.GetServerStatus(gameServerDto.GameServerId);

            if (!rconQueryApiResponse.IsSuccess || rconQueryApiResponse.Result == null)
                throw new NullReferenceException($"Failed to retrieve rcon query result for game server {gameServerDto.GameServerId}");

            var livePlayerDtos = new List<CreateLivePlayerDto>();
            foreach (var rconPlayer in rconQueryApiResponse.Result.Players)
            {
                var livePlayerDto = new CreateLivePlayerDto
                {
                    Name = rconPlayer.Name,
                    Ping = rconPlayer.Ping,
                    Num = rconPlayer.Num,
                    Rate = rconPlayer.Rate,
                    IpAddress = rconPlayer.IpAddress,
                    GameType = gameServerDto.GameType,
                    GameServerId = gameServerDto.GameServerId
                };

                if (!string.IsNullOrWhiteSpace(rconPlayer.Guid))
                {
                    var playerId = await GetPlayerId(gameServerDto.GameType, rconPlayer.Guid);
                    if (playerId.HasValue)
                    {
                        livePlayerDto.PlayerId = playerId;
                    }
                    else if (!string.IsNullOrWhiteSpace(rconPlayer.Name))
                    {
                        var player = new CreatePlayerDto(rconPlayer.Name, rconPlayer.Guid, gameServerDto.GameType)
                        {
                            IpAddress = rconPlayer.IpAddress
                        };

                        await repositoryApiClient.Players.CreatePlayer(player);

                        playerId = await GetPlayerId(gameServerDto.GameType, rconPlayer.Guid);
                        if (playerId.HasValue)
                        {
                            livePlayerDto.PlayerId = playerId;
                        }
                    }
                }

                livePlayerDtos.Add(livePlayerDto);
            }

            return livePlayerDtos;
        }

        private async Task<List<CreateLivePlayerDto>> UpdateLivePlayersFromQuery(GameServerDto gameServerDto, List<CreateLivePlayerDto> livePlayerDtos)
        {
            var serverQueryApiResponse = await serversApiClient.Query.GetServerStatus(gameServerDto.GameServerId);

            if (!serverQueryApiResponse.IsSuccess || serverQueryApiResponse.Result == null)
                throw new NullReferenceException($"Failed to retrieve server query result for game server {gameServerDto.GameServerId}");

            foreach (var livePlayerDto in livePlayerDtos)
            {
                var queryPlayer = serverQueryApiResponse.Result.Players.SingleOrDefault(qp => qp.Name?.NormalizeName() == livePlayerDto.Name?.NormalizeName());

                if (queryPlayer != null)
                {
                    livePlayerDto.Score = queryPlayer.Score;
                }
            }

            var editGameServerDto = new EditGameServerDto(gameServerDto.GameServerId)
            {
                LiveTitle = serverQueryApiResponse.Result.ServerName,
                LiveMap = serverQueryApiResponse.Result.Map,
                LiveMod = serverQueryApiResponse.Result.Mod,
                LiveMaxPlayers = serverQueryApiResponse.Result.MaxPlayers,
                LiveCurrentPlayers = serverQueryApiResponse.Result.PlayerCount,
                LiveLastUpdated = DateTime.UtcNow
            };

            await repositoryApiClient.GameServers.UpdateGameServer(editGameServerDto);

            return livePlayerDtos;
        }

        private async Task<List<CreateLivePlayerDto>> EnrichPlayersWithGeoLocation(List<CreateLivePlayerDto> livePlayerDtos)
        {
            foreach (var livePlayerDto in livePlayerDtos)
            {
                if (string.IsNullOrWhiteSpace(livePlayerDto.IpAddress))
                    continue;

                var lookupAddressResponse = await geoLocationClient.GeoLookup.GetGeoLocation(livePlayerDto.IpAddress);

                if (lookupAddressResponse.IsSuccess && lookupAddressResponse.Result != null)
                {
                    livePlayerDto.Lat = lookupAddressResponse.Result.Latitude;
                    livePlayerDto.Long = lookupAddressResponse.Result.Longitude;
                    livePlayerDto.CountryCode = lookupAddressResponse.Result.CountryCode;
                }
                else
                {
                    lookupAddressResponse.Errors.ForEach(ex => telemetryClient.TrackException(new ApplicationException(ex)));
                }
            }

            return livePlayerDtos;
        }

        private async Task UpdateRecentPlayersWithLivePlayers(List<CreateLivePlayerDto> livePlayerDtos)
        {
            var createRecentPlayerDtos = new List<CreateRecentPlayerDto>();

            foreach (var livePlayer in livePlayerDtos)
            {
                if (string.IsNullOrWhiteSpace(livePlayer.Name) || !livePlayer.PlayerId.HasValue)
                    continue;

                var createRecentPlayerDto = new CreateRecentPlayerDto(livePlayer.Name, livePlayer.GameType, (Guid)livePlayer.PlayerId)
                {
                    IpAddress = livePlayer.IpAddress,
                    Lat = livePlayer.Lat,
                    Long = livePlayer.Long,
                    CountryCode = livePlayer.CountryCode,
                    GameServerId = livePlayer.GameServerId
                };

                createRecentPlayerDtos.Add(createRecentPlayerDto);
            }

            if (createRecentPlayerDtos.Any())
                await repositoryApiClient.RecentPlayers.CreateRecentPlayers(createRecentPlayerDtos);
        }

        private async Task<Guid?> GetPlayerId(GameType gameType, string guid)
        {
            var cacheKey = $"{gameType}-${guid}";

            if (memoryCache.TryGetValue(cacheKey, out Guid playerId))
                return playerId;

            var playerDtoApiResponse = await repositoryApiClient.Players.GetPlayerByGameType(gameType, guid, PlayerEntityOptions.None);

            if (playerDtoApiResponse.IsSuccess && playerDtoApiResponse.Result != null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                memoryCache.Set(cacheKey, playerDtoApiResponse.Result.PlayerId, cacheEntryOptions);

                return playerDtoApiResponse.Result.PlayerId;
            }

            return null;
        }
    }
}
