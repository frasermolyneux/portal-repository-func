using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using MX.GeoLocation.GeoLocationApi.Client;

using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.AdminActions;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.GameServers;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.Players;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.RecentPlayers;
using XtremeIdiots.Portal.RepositoryApiClient.V1;
using XtremeIdiots.Portal.Repository.App.Extensions;
using XtremeIdiots.Portal.ServersApiClient;

namespace XtremeIdiots.Portal.Repository.App.Functions
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
            var gameServersApiResponse = await repositoryApiClient.GameServers.V1.GetGameServers(gameTypes, null, GameServerFilter.LiveTrackingEnabled, 0, 50, null);

            if (!gameServersApiResponse.IsSuccess || gameServersApiResponse.Result == null)
            {
                logger.LogCritical("Failed to retrieve game servers from repository");
                return;
            }

            foreach (var gameServerDto in gameServersApiResponse.Result.Entries)
            {
                using (logger.BeginScope(gameServerDto.TelemetryProperties))
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

                            // Check protected names after players have been loaded
                            await CheckProtectedNameViolations(gameServerDto, livePlayerDtos);

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

                    telemetryClient.TrackMetric("PlayerCount", livePlayerDtos.Count, gameServerDto.TelemetryProperties);

                    await repositoryApiClient.LivePlayers.V1.SetLivePlayersForGameServer(gameServerDto.GameServerId, livePlayerDtos);
                }
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

                        await repositoryApiClient.Players.V1.CreatePlayer(player);

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

            await repositoryApiClient.GameServers.V1.UpdateGameServer(editGameServerDto);

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
                await repositoryApiClient.RecentPlayers.V1.CreateRecentPlayers(createRecentPlayerDtos);
        }

        private async Task<Guid?> GetPlayerId(GameType gameType, string guid)
        {
            var cacheKey = $"{gameType}-${guid}";

            if (memoryCache.TryGetValue(cacheKey, out Guid playerId))
                return playerId;

            var playerDtoApiResponse = await repositoryApiClient.Players.V1.GetPlayerByGameType(gameType, guid, PlayerEntityOptions.None);

            if (playerDtoApiResponse.IsSuccess && playerDtoApiResponse.Result != null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                memoryCache.Set(cacheKey, playerDtoApiResponse.Result.PlayerId, cacheEntryOptions);

                return playerDtoApiResponse.Result.PlayerId;
            }

            return null;
        }

        /// <summary>
        /// Checks if any live players are using protected names that don't belong to them,
        /// and applies appropriate admin actions if a violation is found.
        /// </summary>
        private async Task CheckProtectedNameViolations(GameServerDto gameServer, List<CreateLivePlayerDto> livePlayerDtos)
        {
            try
            {
                // Get all protected names from the repository (with a reasonable limit)
                var protectedNamesResponse = await repositoryApiClient.Players.V1.GetProtectedNames(0, 1000);

                if (!protectedNamesResponse.IsSuccess || protectedNamesResponse.Result == null)
                {
                    logger.LogWarning("Failed to retrieve protected names from repository");
                    return;
                }

                // Check each live player against protected names
                foreach (var livePlayerDto in livePlayerDtos)
                {
                    if (string.IsNullOrEmpty(livePlayerDto.Name) || !livePlayerDto.PlayerId.HasValue)
                        continue;

                    var playerName = livePlayerDto.Name.Trim().ToLower();

                    // Find any protected name that matches the player's current name
                    foreach (var protectedName in protectedNamesResponse.Result.Entries)
                    {
                        if (playerName.Contains(protectedName.Name.ToLower()) ||
                            protectedName.Name.ToLower().Contains(playerName))
                        {
                            // If the player is not the owner of this protected name
                            if (livePlayerDto.PlayerId != protectedName.PlayerId)
                            {
                                // Get the player record to include in the admin action
                                var playerResponse = await repositoryApiClient.Players.V1.GetPlayer(livePlayerDto.PlayerId.Value, PlayerEntityOptions.None);
                                if (!playerResponse.IsSuccess || playerResponse.Result == null)
                                    continue;

                                // Get the owner player's record for reference
                                var ownerResponse = await repositoryApiClient.Players.V1.GetPlayer(protectedName.PlayerId, PlayerEntityOptions.None);
                                if (!ownerResponse.IsSuccess || ownerResponse.Result == null)
                                    continue;

                                logger.LogInformation($"Protected name violation: Player {playerResponse.Result.Username} ({livePlayerDto.PlayerId}) " +
                                                     $"is using protected name '{protectedName.Name}' owned by {ownerResponse.Result.Username} ({protectedName.PlayerId})");

                                // Create an admin action ban for the violating player
                                var adminAction = new CreateAdminActionDto(
                                    livePlayerDto.PlayerId.Value,
                                    AdminActionType.Ban,
                                    $"Protected Name Violation - using '{protectedName.Name}' which is registered to {ownerResponse.Result.Username}"
                                );

                                await repositoryApiClient.AdminActions.V1.CreateAdminAction(adminAction);

                                if (livePlayerDto.Num != 0)
                                {
                                    // If the player is in-game, kick them from the server
                                    var banResponse = await serversApiClient.Rcon.BanPlayer(gameServer.GameServerId, livePlayerDto.Num);
                                    if (!banResponse.IsSuccess)
                                    {
                                        logger.LogWarning($"Failed to ban player {playerResponse.Result.Username} from server {gameServer.GameServerId}");
                                    }
                                }

                                // The player has been banned through the repository
                                // Future implementation would kick the player from the server as well
                                telemetryClient.TrackEvent("ProtectedNameViolation", new Dictionary<string, string> {
                                    { "ViolatingPlayerId", livePlayerDto.PlayerId.ToString() },
                                    { "ViolatingPlayerName", playerResponse.Result.Username },
                                    { "OwnerPlayerId", protectedName.PlayerId.ToString() },
                                    { "OwnerPlayerName", ownerResponse.Result.Username },
                                    { "ProtectedName", protectedName.Name },
                                    { "GameServerName", gameServer.Title },
                                    { "GameServerId", gameServer.GameServerId.ToString() }
                                });

                                break; // Move to the next player once we've banned this one
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking for protected name violations");
            }
        }
    }
}
