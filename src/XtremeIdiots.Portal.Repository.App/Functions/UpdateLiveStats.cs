using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using MX.GeoLocation.Api.Client.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.AdminActions;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Players;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.RecentPlayers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Portal.Repository.App.Extensions;

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
            var gameServersApiResponse = await repositoryApiClient.GameServers.V1.GetGameServers(gameTypes, null, GameServerFilter.LiveTrackingEnabled, 0, 50, null).ConfigureAwait(false);

            if (!gameServersApiResponse.IsSuccess || gameServersApiResponse.Result?.Data?.Items == null)
            {
                logger.LogCritical("Failed to retrieve game servers from repository");
                return;
            }

            foreach (var gameServerDto in gameServersApiResponse.Result.Data.Items)
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
                            livePlayerDtos = await UpdateLivePlayersFromRcon(gameServerDto).ConfigureAwait(false);
                            livePlayerDtos = await UpdateLivePlayersFromQuery(gameServerDto, livePlayerDtos).ConfigureAwait(false);
                            livePlayerDtos = await EnrichPlayersWithGeoLocation(livePlayerDtos).ConfigureAwait(false);

                            // Check protected names after players have been loaded
                            await CheckProtectedNameViolations(gameServerDto, livePlayerDtos).ConfigureAwait(false);

                            await UpdateRecentPlayersWithLivePlayers(livePlayerDtos).ConfigureAwait(false);
                        }
                        else
                        {
                            livePlayerDtos = await UpdateLivePlayersFromQuery(gameServerDto, livePlayerDtos).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"Failed to update live stats for server '{gameServerDto.GameServerId}'");
                        continue;
                    }

                    telemetryClient.TrackMetric("PlayerCount", livePlayerDtos.Count, gameServerDto.TelemetryProperties);

                    await repositoryApiClient.LivePlayers.V1.SetLivePlayersForGameServer(gameServerDto.GameServerId, livePlayerDtos).ConfigureAwait(false);
                }
            }
        }

        private async Task<List<CreateLivePlayerDto>> UpdateLivePlayersFromRcon(GameServerDto gameServerDto)
        {
            var getServerStatusResult = await serversApiClient.Rcon.V1.GetServerStatus(gameServerDto.GameServerId).ConfigureAwait(false);

            if (!getServerStatusResult.IsSuccess || getServerStatusResult.Result?.Data == null)
                throw new NullReferenceException($"Failed to retrieve rcon query result for game server {gameServerDto.GameServerId}");

            var livePlayerDtos = new List<CreateLivePlayerDto>();
            foreach (var rconPlayer in getServerStatusResult.Result.Data.Players)
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
                    var playerId = await GetPlayerId(gameServerDto.GameType, rconPlayer.Guid).ConfigureAwait(false);
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

                        await repositoryApiClient.Players.V1.CreatePlayer(player).ConfigureAwait(false);

                        playerId = await GetPlayerId(gameServerDto.GameType, rconPlayer.Guid).ConfigureAwait(false);
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
            var serverQueryApiResponse = await serversApiClient.Query.V1.GetServerStatus(gameServerDto.GameServerId).ConfigureAwait(false);

            if (!serverQueryApiResponse.IsSuccess || serverQueryApiResponse.Result?.Data == null)
                throw new NullReferenceException($"Failed to retrieve server query result for game server {gameServerDto.GameServerId}");

            foreach (var livePlayerDto in livePlayerDtos)
            {
                var queryPlayer = serverQueryApiResponse.Result.Data.Players.SingleOrDefault(qp => qp.Name?.NormalizeName() == livePlayerDto.Name?.NormalizeName());

                if (queryPlayer != null)
                {
                    livePlayerDto.Score = queryPlayer.Score;
                }
            }

            var editGameServerDto = new EditGameServerDto(gameServerDto.GameServerId)
            {
                LiveTitle = serverQueryApiResponse.Result.Data.ServerName,
                LiveMap = serverQueryApiResponse.Result.Data.Map,
                LiveMod = serverQueryApiResponse.Result.Data.Mod,
                LiveMaxPlayers = serverQueryApiResponse.Result.Data.MaxPlayers,
                LiveCurrentPlayers = serverQueryApiResponse.Result.Data.PlayerCount,
                LiveLastUpdated = DateTime.UtcNow
            };

            await repositoryApiClient.GameServers.V1.UpdateGameServer(editGameServerDto).ConfigureAwait(false);

            return livePlayerDtos;
        }

        private async Task<List<CreateLivePlayerDto>> EnrichPlayersWithGeoLocation(List<CreateLivePlayerDto> livePlayerDtos)
        {
            foreach (var livePlayerDto in livePlayerDtos)
            {
                if (string.IsNullOrWhiteSpace(livePlayerDto.IpAddress))
                    continue;

                var getGeoLocationResult = await geoLocationClient.GeoLookup.V1.GetGeoLocation(livePlayerDto.IpAddress).ConfigureAwait(false);

                if (getGeoLocationResult.IsSuccess && getGeoLocationResult.Result?.Data != null)
                {
                    livePlayerDto.Lat = getGeoLocationResult.Result.Data.Latitude;
                    livePlayerDto.Long = getGeoLocationResult.Result.Data.Longitude;
                    livePlayerDto.CountryCode = getGeoLocationResult.Result.Data.CountryCode;
                }
                else
                {
                    if (getGeoLocationResult.Result?.Errors != null)
                    {
                        foreach (var error in getGeoLocationResult.Result.Errors)
                        {
                            telemetryClient.TrackException(new ApplicationException(error.Message));
                        }
                    }
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
                await repositoryApiClient.RecentPlayers.V1.CreateRecentPlayers(createRecentPlayerDtos).ConfigureAwait(false);
        }

        private async Task<Guid?> GetPlayerId(GameType gameType, string guid)
        {
            var cacheKey = $"{gameType}-${guid}";

            if (memoryCache.TryGetValue(cacheKey, out Guid playerId))
                return playerId;

            var playerDtoApiResponse = await repositoryApiClient.Players.V1.GetPlayerByGameType(gameType, guid, PlayerEntityOptions.None).ConfigureAwait(false);

            if (playerDtoApiResponse.IsSuccess && playerDtoApiResponse.Result?.Data != null)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
                memoryCache.Set(cacheKey, playerDtoApiResponse.Result.Data.PlayerId, cacheEntryOptions);

                return playerDtoApiResponse.Result.Data.PlayerId;
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
                var protectedNamesResponse = await repositoryApiClient.Players.V1.GetProtectedNames(0, 1000).ConfigureAwait(false);

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
                    foreach (var protectedName in protectedNamesResponse.Result?.Data?.Items ?? Enumerable.Empty<ProtectedNameDto>())
                    {
                        if (playerName.Contains(protectedName.Name.ToLower()) ||
                            protectedName.Name.ToLower().Contains(playerName))
                        {
                            // If the player is not the owner of this protected name
                            if (livePlayerDto.PlayerId != protectedName.PlayerId)
                            {
                                // Get the player record to include in the admin action
                                var playerResponse = await repositoryApiClient.Players.V1.GetPlayer(livePlayerDto.PlayerId.Value, PlayerEntityOptions.None).ConfigureAwait(false);
                                if (!playerResponse.IsSuccess || playerResponse.Result == null)
                                    continue;

                                // Get the owner player's record for reference
                                var ownerResponse = await repositoryApiClient.Players.V1.GetPlayer(protectedName.PlayerId, PlayerEntityOptions.None).ConfigureAwait(false);
                                if (!ownerResponse.IsSuccess || ownerResponse.Result == null)
                                    continue;

                                logger.LogInformation($"Protected name violation: Player {playerResponse.Result.Data?.Username} ({livePlayerDto.PlayerId}) " +
                                                     $"is using protected name '{protectedName.Name}' owned by {ownerResponse.Result.Data?.Username} ({protectedName.PlayerId})");

                                // Create an admin action ban for the violating player
                                var adminAction = new CreateAdminActionDto(
                                    livePlayerDto.PlayerId.Value,
                                    AdminActionType.Ban,
                                    $"Protected Name Violation - using '{protectedName.Name}' which is registered to {ownerResponse.Result.Data?.Username}"
                                );

                                await repositoryApiClient.AdminActions.V1.CreateAdminAction(adminAction).ConfigureAwait(false);

                                if (livePlayerDto.Num != 0)
                                {
                                    // If the player is in-game, kick them from the server
                                    var banResponse = await serversApiClient.Rcon.V1.BanPlayer(gameServer.GameServerId, livePlayerDto.Num).ConfigureAwait(false);
                                    if (!banResponse.IsSuccess)
                                    {
                                        logger.LogWarning($"Failed to kick player {playerResponse.Result.Data?.Username} from server {gameServer.GameServerId}");
                                    }
                                    else
                                    {
                                        logger.LogInformation($"Successfully kicked player {playerResponse.Result.Data?.Username} from server {gameServer.GameServerId}");
                                    }
                                }

                                // The player has been banned through the repository
                                // Future implementation would kick the player from the server as well
                                telemetryClient.TrackEvent("ProtectedNameViolation", new Dictionary<string, string> {
                                    { "ViolatingPlayerId", livePlayerDto.PlayerId?.ToString() ?? "Unknown" },
                                    { "ViolatingPlayerName", playerResponse.Result?.Data?.Username ?? "Unknown" },
                                    { "OwnerPlayerId", protectedName.PlayerId.ToString() },
                                    { "OwnerPlayerName", ownerResponse.Result?.Data?.Username ?? "Unknown" },
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
