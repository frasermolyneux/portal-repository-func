using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Moq;

using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Portal.Repository.App.Functions;
using XtremeIdiots.Portal.Repository.App.Services;

namespace XtremeIdiots.Portal.Repository.App.Tests.Functions;

public class DataMaintenanceLoggingTests
{
    private const string StartMessage = "Reconciling VPN detected player tags";
    private const string StartMessageTemplate = "Reconciling VPN detected player tags";
    private const string CompletionMessageTemplate =
        "VPN detected tag reconciliation completed. Candidates: {Candidates}; Players: {Players}; Added: {TagsAdded}; Removed: {TagsRemoved}; Skipped: {PlayersSkipped}";

    [Fact]
    public async Task RunReconcileVpnDetectedTags_WhenInformationLoggingEnabled_LogsBeforeReconcilingAndForwardsTimerArguments()
    {
        var logger = new Mock<ILogger<DataMaintenance>>();
        _ = logger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
        var reconciler = new Mock<IVpnDetectedTagReconciler>();
        var functionContext = new Mock<FunctionContext>();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _ = functionContext.Setup(x => x.CancellationToken).Returns(cancellationToken);
        _ = reconciler
            .Setup(x => x.ReconcileAsync(false, It.IsAny<CancellationToken>()))
            .Callback<bool, CancellationToken>((force, token) =>
            {
                Assert.False(force);
                Assert.Equal(cancellationToken, token);
                AssertStartLog(logger);
            })
            .ReturnsAsync(new VpnDetectedTagReconciliationSummary(10, 8, 3, 2, 1));
        var sut = CreateSut(logger, reconciler);

        await sut.RunReconcileVpnDetectedTags(null, functionContext.Object);

        reconciler.Verify(x => x.ReconcileAsync(false, cancellationToken), Times.Once);
        AssertStartLog(logger);
    }

    [Fact]
    public async Task RunReconcileVpnDetectedTags_WhenInformationLoggingDisabled_StillReconciles()
    {
        var logger = new Mock<ILogger<DataMaintenance>>();
        _ = logger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(false);
        var reconciler = new Mock<IVpnDetectedTagReconciler>();
        var functionContext = new Mock<FunctionContext>();
        var cancellationToken = new CancellationToken(canceled: true);
        _ = functionContext.Setup(x => x.CancellationToken).Returns(cancellationToken);
        _ = reconciler
            .Setup(x => x.ReconcileAsync(false, cancellationToken))
            .ReturnsAsync(new VpnDetectedTagReconciliationSummary(0, 0, 0, 0, 0));
        var sut = CreateSut(logger, reconciler);

        await sut.RunReconcileVpnDetectedTags(null, functionContext.Object);

        reconciler.Verify(x => x.ReconcileAsync(false, cancellationToken), Times.Once);
        Assert.DoesNotContain(logger.Invocations, invocation =>
            invocation.Method.Name == "Log"
            && invocation.Arguments[2]?.ToString() == StartMessage);
    }

    [Fact]
    public async Task RunReconcileVpnDetectedTags_WhenReconcilerFails_RetainsStartLogAndDoesNotLogCompletion()
    {
        var logger = new Mock<ILogger<DataMaintenance>>();
        _ = logger.Setup(x => x.IsEnabled(LogLevel.Information)).Returns(true);
        var reconciler = new Mock<IVpnDetectedTagReconciler>();
        var functionContext = new Mock<FunctionContext>();
        _ = functionContext.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
        var expectedException = new InvalidOperationException("reconciliation failed");
        _ = reconciler
            .Setup(x => x.ReconcileAsync(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);
        var sut = CreateSut(logger, reconciler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RunReconcileVpnDetectedTags(null, functionContext.Object));

        Assert.Same(expectedException, exception);
        AssertStartLog(logger);
        Assert.DoesNotContain(logger.Invocations, invocation =>
            invocation.Method.Name == "Log"
            && invocation.Arguments[2] is IEnumerable<KeyValuePair<string, object?>> fields
            && fields.Any(field => field.Key == "{OriginalFormat}"
                && field.Value?.ToString() == CompletionMessageTemplate));
    }

    private static DataMaintenance CreateSut(
        Mock<ILogger<DataMaintenance>> logger,
        Mock<IVpnDetectedTagReconciler> reconciler)
    {
        return new DataMaintenance(
            logger.Object,
            Mock.Of<IRepositoryApiClient>(),
            new ConfigurationBuilder().Build(),
            reconciler.Object);
    }

    private static void AssertStartLog(Mock<ILogger<DataMaintenance>> logger)
    {
        var startLog = Assert.Single(logger.Invocations, invocation =>
            invocation.Method.Name == "Log"
            && invocation.Arguments[0] is LogLevel.Information
            && invocation.Arguments[1] is EventId eventId
            && eventId.Id == 0
            && invocation.Arguments[2]?.ToString() == StartMessage);

        var eventId = Assert.IsType<EventId>(startLog.Arguments[1]);
        Assert.Null(eventId.Name);
        Assert.Null(startLog.Arguments[3]);

        var fields = Assert.IsType<IEnumerable<KeyValuePair<string, object?>>>(startLog.Arguments[2], exactMatch: false)
            .ToDictionary(field => field.Key, field => field.Value);
        Assert.Equal(StartMessageTemplate, fields["{OriginalFormat}"]);
    }
}
