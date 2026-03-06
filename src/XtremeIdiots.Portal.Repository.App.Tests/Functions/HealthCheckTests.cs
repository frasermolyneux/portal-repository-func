using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Moq;

using XtremeIdiots.Portal.Repository.App.Functions;

namespace XtremeIdiots.Portal.Repository.App.Tests.Functions;

public class HealthCheckTests
{
    [Fact]
    public async Task Run_WhenHealthy_ReturnsHealthyStatus()
    {
        var healthCheckServiceMock = new Mock<HealthCheckService>();
        var healthReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(),
            TimeSpan.Zero);
        healthCheckServiceMock
            .Setup(x => x.CheckHealthAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(healthReport);

        var sut = new HealthCheck(healthCheckServiceMock.Object);
        // HttpRequestData and FunctionContext are not used by the implementation, so null! is acceptable
        var result = await sut.Run(null!, null!);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Healthy", okResult.Value);
        healthCheckServiceMock.Verify(x => x.CheckHealthAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_WhenUnhealthy_ReturnsUnhealthyStatus()
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

        var sut = new HealthCheck(healthCheckServiceMock.Object);
        // HttpRequestData and FunctionContext are not used by the implementation, so null! is acceptable
        var result = await sut.Run(null!, null!);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Unhealthy", okResult.Value);
        healthCheckServiceMock.Verify(x => x.CheckHealthAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_WhenDegraded_ReturnsDegradedStatus()
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

        var sut = new HealthCheck(healthCheckServiceMock.Object);
        // HttpRequestData and FunctionContext are not used by the implementation, so null! is acceptable
        var result = await sut.Run(null!, null!);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Degraded", okResult.Value);
        healthCheckServiceMock.Verify(x => x.CheckHealthAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
