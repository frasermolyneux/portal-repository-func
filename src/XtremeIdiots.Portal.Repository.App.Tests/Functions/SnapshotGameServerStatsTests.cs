using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using Moq;

using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;
using XtremeIdiots.Portal.Repository.Api.Client.Testing;
using XtremeIdiots.Portal.Repository.App.Functions;

namespace XtremeIdiots.Portal.Repository.App.Tests.Functions;

public class SnapshotGameServerStatsTests
{
    private readonly Mock<ILogger<SnapshotGameServerStats>> _loggerMock = new();
    private readonly FakeRepositoryApiClient _fakeRepositoryApiClient = new();
    private readonly FakeServersApiClient _fakeServersApiClient = new();
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

    private SnapshotGameServerStats CreateSut() => new(
        _loggerMock.Object,
        _fakeRepositoryApiClient,
        _fakeServersApiClient,
        _memoryCache
    );

    [Fact]
    public async Task RunSnapshotGameServerStats_WhenNoServers_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunSnapshotGameServerStats(new TimerInfo());
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        var sut = CreateSut();

        Assert.NotNull(sut);
    }
}
