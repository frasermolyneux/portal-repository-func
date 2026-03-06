using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Moq;

using XtremeIdiots.Portal.Repository.Api.Client.Testing;
using XtremeIdiots.Portal.Repository.App.Functions;

namespace XtremeIdiots.Portal.Repository.App.Tests.Functions;

public class UpdateBanFileMonitorConfigTests
{
    private readonly Mock<ILogger<UpdateBanFileMonitorConfig>> _loggerMock = new();
    private readonly FakeRepositoryApiClient _fakeRepositoryApiClient = new();
    private readonly IConfiguration _configuration;
    private readonly TelemetryClient _telemetryClient;

    public UpdateBanFileMonitorConfigTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["XtremeIdiots:FtpCertificateThumbprint"] = "test-thumbprint"
            })
            .Build();

        _telemetryClient = new TelemetryClient(new TelemetryConfiguration());
    }

    private UpdateBanFileMonitorConfig CreateSut() => new(
        _loggerMock.Object,
        _fakeRepositoryApiClient,
        _configuration,
        _telemetryClient
    );

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        var sut = CreateSut();

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task RunUpdateBanFileMonitorConfig_WhenNoServers_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunUpdateBanFileMonitorConfig(null);
    }
}
