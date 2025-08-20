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
    public class UpdateLiveLogFile
    {
        private readonly ILogger<UpdateLiveLogFile> logger;
        private readonly IRepositoryApiClient repositoryApiClient;
        private readonly IConfiguration configuration;
        private readonly TelemetryClient telemetryClient;

        public UpdateLiveLogFile(
            ILogger<UpdateLiveLogFile> logger,
            IRepositoryApiClient repositoryApiClient,
            IConfiguration configuration,
            TelemetryClient telemetryClient)
        {
            this.logger = logger;
            this.repositoryApiClient = repositoryApiClient;
            this.configuration = configuration;
            this.telemetryClient = telemetryClient;
        }

        [Function(nameof(RunUpdateLiveLogFileManual))]
        public async Task<HttpResponseData> RunUpdateLiveLogFileManual([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestData req)
        {
            await RunUpdateLiveLogFile(null);
            return req.CreateResponse(HttpStatusCode.OK);
        }

        [Function(nameof(RunUpdateLiveLogFile))]
        public async Task RunUpdateLiveLogFile([TimerTrigger("0 0 */1 * * *")] TimerInfo? myTimer)
        {
            GameType[] gameTypes = [GameType.CallOfDuty2, GameType.CallOfDuty4, GameType.CallOfDuty5];
            var gameServersApiResponse = await repositoryApiClient.GameServers.V1.GetGameServers(gameTypes, null, null, 0, 50, null);

            if (!gameServersApiResponse.IsSuccess || gameServersApiResponse.Result == null)
            {
                logger.LogCritical("Failed to retrieve game servers from repository");
                return;
            }

            var validGameServers = gameServersApiResponse.Result.Data?.Items?.Where(gs => !string.IsNullOrWhiteSpace(gs.LiveMod) && !string.IsNullOrWhiteSpace(gs.FtpHostname) && !string.IsNullOrWhiteSpace(gs.FtpUsername) && !string.IsNullOrWhiteSpace(gs.FtpPassword)).ToList() ?? new List<GameServerDto>();

            foreach (var gameServerDto in validGameServers)
            {
                using (logger.BeginScope(gameServerDto.TelemetryProperties))
                {
                    AsyncFtpClient? ftpClient = null;
                    try
                    {
                        ftpClient = new AsyncFtpClient(gameServerDto.FtpHostname, gameServerDto.FtpUsername, gameServerDto.FtpPassword, gameServerDto.FtpPort ?? 21);
                        ftpClient.ValidateCertificate += (control, e) =>
                        {
                            if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                            { // Account for self-signed FTP certificate for self-hosted servers
                                e.Accept = true;
                            }
                        };

                        await ftpClient.AutoConnect();
                        await ftpClient.SetWorkingDirectory(gameServerDto.LiveMod);

                        var files = await ftpClient.GetListing();

                        var active = files.Where(f => f.Name.Contains(".log") && !f.Name.Contains("console")).OrderByDescending(f => f.Modified).FirstOrDefault();
                        if (active != null)
                        {
                            await repositoryApiClient.GameServers.V1.UpdateGameServer(new EditGameServerDto(gameServerDto.GameServerId)
                            {
                                LiveLogFile = active.FullName
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        telemetryClient.TrackException(ex);
                        continue;
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
