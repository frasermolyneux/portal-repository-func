using System.Net;

using FluentFTP;
using FluentFTP.Logging;

using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.BanFileMonitors;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Repository.App.Functions
{
    public class UpdateBanFileMonitorConfig
    {
        private readonly ILogger<UpdateBanFileMonitorConfig> logger;
        private readonly IRepositoryApiClient repositoryApiClient;
        private readonly IConfiguration configuration;
        private readonly TelemetryClient telemetryClient;

        public UpdateBanFileMonitorConfig(
            ILogger<UpdateBanFileMonitorConfig> logger,
            IRepositoryApiClient repositoryApiClient,
            IConfiguration configuration,
            TelemetryClient telemetryClient)
        {
            this.logger = logger;
            this.repositoryApiClient = repositoryApiClient;
            this.configuration = configuration;
            this.telemetryClient = telemetryClient;
        }

        [Function(nameof(RunUpdateBanFileMonitorConfigManual))]
        public async Task<HttpResponseData> RunUpdateBanFileMonitorConfigManual([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req)
        {
            await RunUpdateBanFileMonitorConfig(null).ConfigureAwait(false);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function(nameof(RunUpdateBanFileMonitorConfig))]
        public async Task RunUpdateBanFileMonitorConfig([TimerTrigger("0 0 */1 * * *")] TimerInfo? myTimer)
        {
            GameType[] gameTypes = [GameType.CallOfDuty2, GameType.CallOfDuty4, GameType.CallOfDuty5];
            var gameServersApiResponse = await repositoryApiClient.GameServers.V1.GetGameServers(gameTypes, null, null, 0, 50, null).ConfigureAwait(false);
            var banFileMonitorsApiResponse = await repositoryApiClient.BanFileMonitors.V1.GetBanFileMonitors(gameTypes, null, null, 0, 50, null).ConfigureAwait(false);

            if (!gameServersApiResponse.IsSuccess || gameServersApiResponse.Result == null)
            {
                logger.LogCritical("Failed to retrieve game servers from repository");
                return;
            }

            if (!banFileMonitorsApiResponse.IsSuccess || banFileMonitorsApiResponse.Result == null)
            {
                logger.LogCritical("Failed to retrieve ban file monitors from repository");
                return;
            }

            foreach (var gameServerDto in gameServersApiResponse.Result?.Data?.Items ?? Enumerable.Empty<GameServerDto>())
            {
                using (logger.BeginScope(gameServerDto.TelemetryProperties))
                {
                    if (string.IsNullOrWhiteSpace(gameServerDto.LiveMod))
                        continue;

                    var banFileMonitorDto = banFileMonitorsApiResponse.Result?.Data?.Items?.SingleOrDefault(bfm => bfm.GameServerId == gameServerDto.GameServerId);

                    if (banFileMonitorDto == null)
                    {
                        if (!string.IsNullOrWhiteSpace(gameServerDto.FtpHostname) && !string.IsNullOrWhiteSpace(gameServerDto.FtpUsername) && !string.IsNullOrWhiteSpace(gameServerDto.FtpPassword) && gameServerDto.FtpPort != null)
                        {
                            logger.LogInformation($"BanFileMonitor for '{gameServerDto.Title}' does not exist - creating");

                            try
                            {
                                await using var ftpClient = new AsyncFtpClient(gameServerDto.FtpHostname, gameServerDto.FtpUsername, gameServerDto.FtpPassword, gameServerDto.FtpPort.Value, logger: new FtpLogAdapter(logger));
                                ftpClient.ValidateCertificate += (control, e) =>
                                {
                                    if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                                    { // Account for self-signed FTP certificate for self-hosted servers
                                        e.Accept = true;
                                    }
                                };

                                await ftpClient.AutoConnect().ConfigureAwait(false);

                                if (await ftpClient.FileExists($"/{gameServerDto.LiveMod}/ban.txt").ConfigureAwait(false))
                                {
                                    var createBanFileMonitorDto = new CreateBanFileMonitorDto(gameServerDto.GameServerId, $"/{gameServerDto.LiveMod}/ban.txt", gameServerDto.GameType);
                                    await repositoryApiClient.BanFileMonitors.V1.CreateBanFileMonitor(createBanFileMonitorDto).ConfigureAwait(false);
                                }
                            }
                            catch (Exception ex)
                            {
                                telemetryClient.TrackException(ex, gameServerDto.TelemetryProperties);
                            }
                        }
                    }
                    else
                    {
                        if (!banFileMonitorDto.FilePath.ToLower().Contains(gameServerDto.LiveMod))
                        {
                            if (!string.IsNullOrWhiteSpace(gameServerDto.FtpHostname) && !string.IsNullOrWhiteSpace(gameServerDto.FtpUsername) && !string.IsNullOrWhiteSpace(gameServerDto.FtpPassword) && gameServerDto.FtpPort != null)
                            {
                                logger.LogInformation($"BanFileMonitor for '{gameServerDto.Title}' does not have current mod in path - updating");

                                try
                                {
                                    await using var ftpClient = new AsyncFtpClient(gameServerDto.FtpHostname, gameServerDto.FtpUsername, gameServerDto.FtpPassword, gameServerDto.FtpPort.Value, logger: new FtpLogAdapter(logger));
                                    ftpClient.ValidateCertificate += (control, e) =>
                                    {
                                        if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                                        { // Account for self-signed FTP certificate for self-hosted servers
                                            e.Accept = true;
                                        }
                                    };

                                    await ftpClient.AutoConnect().ConfigureAwait(false);

                                    if (await ftpClient.DirectoryExists(gameServerDto.LiveMod).ConfigureAwait(false))
                                    {
                                        var editBanFileMonitorDto = new EditBanFileMonitorDto(banFileMonitorDto.BanFileMonitorId, $"/{gameServerDto.LiveMod}/ban.txt");
                                        await repositoryApiClient.BanFileMonitors.V1.UpdateBanFileMonitor(editBanFileMonitorDto).ConfigureAwait(false);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    telemetryClient.TrackException(ex, banFileMonitorDto.TelemetryProperties);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
