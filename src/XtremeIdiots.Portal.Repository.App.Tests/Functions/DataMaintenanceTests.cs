using Microsoft.Extensions.Logging;

using Moq;

using XtremeIdiots.Portal.Repository.Api.Client.Testing;
using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Portal.Repository.App.Functions;

namespace XtremeIdiots.Portal.Repository.App.Tests.Functions;

public class DataMaintenanceTests
{
    private readonly Mock<ILogger<DataMaintenance>> _loggerMock = new();
    private readonly FakeRepositoryApiClient _fakeRepositoryApiClient = new();

    private DataMaintenance CreateSut() => new(
        _loggerMock.Object,
        _fakeRepositoryApiClient
    );

    [Fact]
    public async Task RunPruneChatMessages_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunPruneChatMessages(null);
    }

    [Fact]
    public async Task RunPruneGameServerEvents_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunPruneGameServerEvents(null);
    }

    [Fact]
    public async Task RunPruneGameServerStats_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunPruneGameServerStats(null);
    }

    [Fact]
    public async Task RunResetSystemAssignedPlayerTags_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunResetSystemAssignedPlayerTags(null);
    }

    [Fact]
    public async Task RunPruneChatMessages_ShouldCallPruneChatMessages()
    {
        var repositoryApiClientMock = new Mock<IRepositoryApiClient> { DefaultValue = DefaultValue.Mock };
        var sut = new DataMaintenance(_loggerMock.Object, repositoryApiClientMock.Object);

        await sut.RunPruneChatMessages(null);

        Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1)
            .Verify(x => x.PruneChatMessages(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunPruneGameServerEvents_ShouldCallPruneGameServerEvents()
    {
        var repositoryApiClientMock = new Mock<IRepositoryApiClient> { DefaultValue = DefaultValue.Mock };
        var sut = new DataMaintenance(_loggerMock.Object, repositoryApiClientMock.Object);

        await sut.RunPruneGameServerEvents(null);

        Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1)
            .Verify(x => x.PruneGameServerEvents(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunPruneGameServerStats_ShouldCallPruneGameServerStats()
    {
        var repositoryApiClientMock = new Mock<IRepositoryApiClient> { DefaultValue = DefaultValue.Mock };
        var sut = new DataMaintenance(_loggerMock.Object, repositoryApiClientMock.Object);

        await sut.RunPruneGameServerStats(null);

        Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1)
            .Verify(x => x.PruneGameServerStats(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunResetSystemAssignedPlayerTags_ShouldCallResetSystemAssignedPlayerTags()
    {
        var repositoryApiClientMock = new Mock<IRepositoryApiClient> { DefaultValue = DefaultValue.Mock };
        var sut = new DataMaintenance(_loggerMock.Object, repositoryApiClientMock.Object);

        await sut.RunResetSystemAssignedPlayerTags(null);

        Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1)
            .Verify(x => x.ResetSystemAssignedPlayerTags(It.IsAny<CancellationToken>()), Times.Once);
    }
}
