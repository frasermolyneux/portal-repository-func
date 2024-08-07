using System.Net;

using FluentFTP;
using FluentFTP.Logging;

using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.BanFileMonitors;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Models.GameServers;
using XtremeIdiots.Portal.RepositoryApiClient;

namespace XtremeIdiots.Portal.RepositoryFunc
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
            var gameServersApiResponse = await repositoryApiClient.GameServers.GetGameServers(gameTypes, null, null, 0, 50, null);

            if (!gameServersApiResponse.IsSuccess || gameServersApiResponse.Result == null)
            {
                logger.LogCritical("Failed to retrieve game servers from repository");
                return;
            }

            var validGameServers = gameServersApiResponse.Result.Entries.Where(gs => !string.IsNullOrWhiteSpace(gs.LiveMod) && !string.IsNullOrWhiteSpace(gs.FtpHostname) && !string.IsNullOrWhiteSpace(gs.FtpUsername) && !string.IsNullOrWhiteSpace(gs.FtpPassword)).ToList();

            foreach (var gameServerDto in validGameServers)
            {
                using (logger.BeginScope(gameServerDto.TelemetryProperties))
                {
                    AsyncFtpClient? ftpClient = null;
                    try
                    {
                        ftpClient = new AsyncFtpClient(gameServerDto.FtpHostname, gameServerDto.FtpUsername, gameServerDto.FtpPassword, gameServerDto.FtpPort.Value);
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
                            await repositoryApiClient.GameServers.UpdateGameServer(new EditGameServerDto(gameServerDto.GameServerId)
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
