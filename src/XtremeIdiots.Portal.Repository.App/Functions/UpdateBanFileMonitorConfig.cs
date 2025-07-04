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
            await RunUpdateBanFileMonitorConfig(null);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function(nameof(RunUpdateBanFileMonitorConfig))]
        public async Task RunUpdateBanFileMonitorConfig([TimerTrigger("0 0 */1 * * *")] TimerInfo? myTimer)
        {
            GameType[] gameTypes = new GameType[] { GameType.CallOfDuty2, GameType.CallOfDuty4, GameType.CallOfDuty5 };
            var gameServersApiResponse = await repositoryApiClient.GameServers.V1.GetGameServers(gameTypes, null, null, 0, 50, null);
            var banFileMonitorsApiResponse = await repositoryApiClient.BanFileMonitors.V1.GetBanFileMonitors(gameTypes, null, null, 0, 50, null);

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

            foreach (var gameServerDto in gameServersApiResponse.Result.Entries)
            {
                using (logger.BeginScope(gameServerDto.TelemetryProperties))
                {
                    if (string.IsNullOrWhiteSpace(gameServerDto.LiveMod))
                        continue;

                    var banFileMonitorDto = banFileMonitorsApiResponse.Result.Entries.SingleOrDefault(bfm => bfm.GameServerId == gameServerDto.GameServerId);

                    if (banFileMonitorDto == null)
                    {
                        if (!string.IsNullOrWhiteSpace(gameServerDto.FtpHostname) && !string.IsNullOrWhiteSpace(gameServerDto.FtpUsername) && !string.IsNullOrWhiteSpace(gameServerDto.FtpPassword) && gameServerDto.FtpPort != null)
                        {
                            logger.LogInformation($"BanFileMonitor for '{gameServerDto.Title}' does not exist - creating");

                            AsyncFtpClient? ftpClient = null;
                            try
                            {
                                ftpClient = new AsyncFtpClient(gameServerDto.FtpHostname, gameServerDto.FtpUsername, gameServerDto.FtpPassword, gameServerDto.FtpPort.Value, logger: new FtpLogAdapter(logger));
                                ftpClient.ValidateCertificate += (control, e) =>
                                {
                                    if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                                    { // Account for self-signed FTP certificate for self-hosted servers
                                        e.Accept = true;
                                    }
                                };

                                await ftpClient.AutoConnect();

                                if (await ftpClient.FileExists($"/{gameServerDto.LiveMod}/ban.txt"))
                                {
                                    var createBanFileMonitorDto = new CreateBanFileMonitorDto(gameServerDto.GameServerId, $"/{gameServerDto.LiveMod}/ban.txt", gameServerDto.GameType);
                                    await repositoryApiClient.BanFileMonitors.V1.CreateBanFileMonitor(createBanFileMonitorDto);
                                }
                            }
                            catch (Exception ex)
                            {
                                telemetryClient.TrackException(ex, gameServerDto.TelemetryProperties);
                            }
                            finally
                            {
                                ftpClient?.Dispose();
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

                                AsyncFtpClient? ftpClient = null;
                                try
                                {
                                    ftpClient = new AsyncFtpClient(gameServerDto.FtpHostname, gameServerDto.FtpUsername, gameServerDto.FtpPassword, gameServerDto.FtpPort.Value, logger: new FtpLogAdapter(logger));
                                    ftpClient.ValidateCertificate += (control, e) =>
                                    {
                                        if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                                        { // Account for self-signed FTP certificate for self-hosted servers
                                            e.Accept = true;
                                        }
                                    };

                                    await ftpClient.AutoConnect();

                                    if (await ftpClient.DirectoryExists(gameServerDto.LiveMod))
                                    {
                                        var editBanFileMonitorDto = new EditBanFileMonitorDto(banFileMonitorDto.BanFileMonitorId, $"/{gameServerDto.LiveMod}/ban.txt");
                                        await repositoryApiClient.BanFileMonitors.V1.UpdateBanFileMonitor(editBanFileMonitorDto);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    telemetryClient.TrackException(ex, banFileMonitorDto.TelemetryProperties);
                                }
                                finally
                                {
                                    ftpClient?.Dispose();
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
