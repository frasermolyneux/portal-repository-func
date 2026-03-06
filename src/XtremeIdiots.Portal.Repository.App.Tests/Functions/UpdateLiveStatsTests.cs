using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Moq;

using MX.GeoLocation.Api.Client.Testing;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;
using XtremeIdiots.Portal.Repository.Api.Client.Testing;
using XtremeIdiots.Portal.Repository.App.Functions;

namespace XtremeIdiots.Portal.Repository.App.Tests.Functions;

public class UpdateLiveStatsTests
{
    private readonly Mock<ILogger<UpdateLiveStats>> _loggerMock = new();
    private readonly FakeRepositoryApiClient _fakeRepositoryApiClient = new();
    private readonly FakeServersApiClient _fakeServersApiClient = new();
    private readonly FakeGeoLocationApiClient _fakeGeoLocationApiClient = new();
    private readonly TelemetryClient _telemetryClient;
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

    public UpdateLiveStatsTests()
    {
        _telemetryClient = new TelemetryClient(new TelemetryConfiguration());
    }

    private UpdateLiveStats CreateSut() => new(
        _loggerMock.Object,
        _fakeRepositoryApiClient,
        _fakeServersApiClient,
        _fakeGeoLocationApiClient,
        _telemetryClient,
        _memoryCache
    );

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        var sut = CreateSut();

        Assert.NotNull(sut);
    }

    [Fact]
    public async Task RunUpdateLiveStats_WhenNoServers_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunUpdateLiveStats(new TimerInfo());
    }
}
