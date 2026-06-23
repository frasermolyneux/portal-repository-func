using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Moq;

using XtremeIdiots.Portal.Repository.App.Functions;

namespace XtremeIdiots.Portal.Repository.App.Tests.Functions;

public class HealthCheckTests
{
    [Fact]
    public async Task RunReady_WhenHealthy_Returns200WithHealthyStatus()
    {
        var healthCheckServiceMock = new Mock<HealthCheckService>();
        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(),
            TimeSpan.Zero);
        healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        var context = new Mock<FunctionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var sut = new HealthCheck(healthCheckServiceMock.Object);
        var result = await sut.RunReady(null!, context.Object);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);

        var statusProperty = objectResult.Value?.GetType().GetProperty("status");
        Assert.NotNull(statusProperty);
        Assert.Equal("Healthy", statusProperty.GetValue(objectResult.Value));

        healthCheckServiceMock.Verify(x => x.CheckHealthAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunReady_WhenUnhealthy_Returns503WithUnhealthyStatus()
    {
        var healthCheckServiceMock = new Mock<HealthCheckService>();
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["test"] = new HealthReportEntry(HealthStatus.Unhealthy, "test", TimeSpan.Zero, null, null)
        };
        var healthReport = new HealthReport(entries, TimeSpan.Zero);
        healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        var context = new Mock<FunctionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var sut = new HealthCheck(healthCheckServiceMock.Object);
        var result = await sut.RunReady(null!, context.Object);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);

        var statusProperty = objectResult.Value?.GetType().GetProperty("status");
        Assert.NotNull(statusProperty);
        Assert.Equal("Unhealthy", statusProperty.GetValue(objectResult.Value));

        healthCheckServiceMock.Verify(x => x.CheckHealthAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunReady_WhenDegraded_Returns503WithDegradedStatus()
    {
        var healthCheckServiceMock = new Mock<HealthCheckService>();
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["test"] = new HealthReportEntry(HealthStatus.Degraded, "degraded", TimeSpan.Zero, null, null)
        };
        var healthReport = new HealthReport(entries, TimeSpan.Zero);
        healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        var context = new Mock<FunctionContext>();
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        var sut = new HealthCheck(healthCheckServiceMock.Object);
        var result = await sut.RunReady(null!, context.Object);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);

        var statusProperty = objectResult.Value?.GetType().GetProperty("status");
        Assert.NotNull(statusProperty);
        Assert.Equal("Degraded", statusProperty.GetValue(objectResult.Value));

        healthCheckServiceMock.Verify(x => x.CheckHealthAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RunLive_ReturnsHealthyStatus()
    {
        var healthCheckServiceMock = new Mock<HealthCheckService>();
        var sut = new HealthCheck(healthCheckServiceMock.Object);

        var result = sut.RunLive(null!);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var statusProperty = okResult.Value?.GetType().GetProperty("status");
        Assert.NotNull(statusProperty);
        Assert.Equal("Healthy", statusProperty.GetValue(okResult.Value));
    }
}
