using System.Net;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

using Moq;

using MX.Api.Abstractions;

using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Portal.Repository.App.Functions;

namespace XtremeIdiots.Portal.Repository.App.Tests.Functions;

public class MapPopularityTests
{
    private readonly Mock<ILogger<MapPopularity>> _loggerMock = new();

    private MapPopularity CreateSut() => new(
        _loggerMock.Object,
        CreateRepositoryApiClientMock().Object
    );

    [Fact]
    public async Task RunRebuildMapPopularity_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunRebuildMapPopularity(new TimerInfo());
    }

    [Fact]
    public async Task RunRebuildMapPopularity_ShouldCallRebuildMapPopularity()
    {
        var repositoryApiClientMock = CreateRepositoryApiClientMock();
        var sut = new MapPopularity(_loggerMock.Object, repositoryApiClientMock.Object);

        await sut.RunRebuildMapPopularity(new TimerInfo());

        Mock.Get(repositoryApiClientMock.Object.Maps.V1)
            .Verify(x => x.RebuildMapPopularity(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IRepositoryApiClient> CreateRepositoryApiClientMock()
    {
        var repositoryApiClientMock = new Mock<IRepositoryApiClient> { DefaultValue = DefaultValue.Mock };
        Mock.Get(repositoryApiClientMock.Object.Maps.V1)
            .Setup(x => x.RebuildMapPopularity(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult(HttpStatusCode.OK));
        return repositoryApiClientMock;
    }
}
