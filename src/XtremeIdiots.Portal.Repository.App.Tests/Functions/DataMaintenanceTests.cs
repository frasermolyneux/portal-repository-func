using System.Net;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker;

using Moq;

using MX.Api.Abstractions;

using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Portal.Repository.App.Functions;
using XtremeIdiots.Portal.Repository.App.Services;

namespace XtremeIdiots.Portal.Repository.App.Tests.Functions;

public class DataMaintenanceTests
{
    private readonly Mock<ILogger<DataMaintenance>> _loggerMock = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();
    private readonly Mock<IVpnDetectedTagReconciler> _vpnDetectedTagReconciler = new();

    private DataMaintenance CreateSut() => new(
        _loggerMock.Object,
        CreateRepositoryApiClientMock().Object,
        _configuration,
        _vpnDetectedTagReconciler.Object
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
    public async Task RunPrunePlayerIpAddresses_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunPrunePlayerIpAddresses(null);
    }

    [Fact]
    public async Task RunResetSystemAssignedPlayerTags_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunResetSystemAssignedPlayerTags(null);
    }

    [Fact]
    public async Task RunReconcileConnectedPlayerTags_ShouldCompleteSuccessfully()
    {
        var sut = CreateSut();

        await sut.RunReconcileConnectedPlayerTags(null);
    }

    [Fact]
    public async Task RunPruneChatMessages_ShouldCallPruneChatMessages()
    {
        var repositoryApiClientMock = CreateRepositoryApiClientMock();
        var sut = new DataMaintenance(_loggerMock.Object, repositoryApiClientMock.Object, _configuration, _vpnDetectedTagReconciler.Object);

        await sut.RunPruneChatMessages(null);

        Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1)
            .Verify(x => x.PruneChatMessages(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunPruneGameServerEvents_ShouldCallPruneGameServerEvents()
    {
        var repositoryApiClientMock = CreateRepositoryApiClientMock();
        var sut = new DataMaintenance(_loggerMock.Object, repositoryApiClientMock.Object, _configuration, _vpnDetectedTagReconciler.Object);

        await sut.RunPruneGameServerEvents(null);

        Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1)
            .Verify(x => x.PruneGameServerEvents(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunPruneGameServerStats_ShouldCallPruneGameServerStats()
    {
        var repositoryApiClientMock = CreateRepositoryApiClientMock();
        var sut = new DataMaintenance(_loggerMock.Object, repositoryApiClientMock.Object, _configuration, _vpnDetectedTagReconciler.Object);

        await sut.RunPruneGameServerStats(null);

        Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1)
            .Verify(x => x.PruneGameServerStats(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunPrunePlayerIpAddresses_ShouldCallPrunePlayerIpAddresses()
    {
        var repositoryApiClientMock = CreateRepositoryApiClientMock();
        var sut = new DataMaintenance(_loggerMock.Object, repositoryApiClientMock.Object, _configuration, _vpnDetectedTagReconciler.Object);

        await sut.RunPrunePlayerIpAddresses(null);

        Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1)
            .Verify(x => x.PrunePlayerIpAddresses(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunResetSystemAssignedPlayerTags_ShouldCallResetSystemAssignedPlayerTags()
    {
        var repositoryApiClientMock = CreateRepositoryApiClientMock();
        var sut = new DataMaintenance(_loggerMock.Object, repositoryApiClientMock.Object, _configuration, _vpnDetectedTagReconciler.Object);

        await sut.RunResetSystemAssignedPlayerTags(null);

        Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1)
            .Verify(x => x.ResetSystemAssignedPlayerTags(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunReconcileConnectedPlayerTags_WithMissingConfig_CompletesWithoutCallingRepositoryClient()
    {
        var repositoryApiClientMock = CreateRepositoryApiClientMock();
        var sut = new DataMaintenance(_loggerMock.Object, repositoryApiClientMock.Object, _configuration, _vpnDetectedTagReconciler.Object);

        await sut.RunReconcileConnectedPlayerTags(null);

        Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1)
            .VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunReconcileVpnDetectedTags_DelegatesToReconciler()
    {
        var functionContext = new Mock<FunctionContext>();
        functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
        _vpnDetectedTagReconciler
            .Setup(x => x.ReconcileAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VpnDetectedTagReconciliationSummary(0, 0, 0, 0, 0));

        await CreateSut().RunReconcileVpnDetectedTags(null, functionContext.Object);

        _vpnDetectedTagReconciler.Verify(x => x.ReconcileAsync(false, CancellationToken.None), Times.Once);
    }

    private static Mock<IRepositoryApiClient> CreateRepositoryApiClientMock()
    {
        var repositoryApiClientMock = new Mock<IRepositoryApiClient> { DefaultValue = DefaultValue.Mock };
        var dataMaintenanceApi = Mock.Get(repositoryApiClientMock.Object.DataMaintenance.V1);
        dataMaintenanceApi.Setup(x => x.PruneChatMessages(It.IsAny<CancellationToken>())).ReturnsAsync(new ApiResult(HttpStatusCode.OK));
        dataMaintenanceApi.Setup(x => x.PruneGameServerEvents(It.IsAny<CancellationToken>())).ReturnsAsync(new ApiResult(HttpStatusCode.OK));
        dataMaintenanceApi.Setup(x => x.PruneGameServerStats(It.IsAny<CancellationToken>())).ReturnsAsync(new ApiResult(HttpStatusCode.OK));
        dataMaintenanceApi.Setup(x => x.PrunePlayerIpAddresses(It.IsAny<CancellationToken>())).ReturnsAsync(new ApiResult(HttpStatusCode.OK));
        dataMaintenanceApi.Setup(x => x.ResetSystemAssignedPlayerTags(It.IsAny<CancellationToken>())).ReturnsAsync(new ApiResult(HttpStatusCode.OK));
        return repositoryApiClientMock;
    }
}
