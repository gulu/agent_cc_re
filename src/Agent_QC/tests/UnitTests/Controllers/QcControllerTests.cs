using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using Agent_QC.Controllers;
using Agent_QC.Models;
using Agent_QC.Services;

namespace Agent_QC.Tests.UnitTests.Controllers;

public class QcControllerTests
{
    [Fact]
    public async Task PostQcReport_MissingReportId_ReturnsError()
    {
        var mockService = new Mock<IQcService>();
        var controller = new QcController(mockService.Object);
        var request = new QcRequest { ReportId = "", Findings = "test", Impression = "test" };

        var result = await controller.PostQcReport(request);
        var objResult = Assert.IsType<OkObjectResult>(result);
        var ajax = Assert.IsType<AjaxResult>(objResult.Value);
        Assert.Equal(400, ajax.Code);
    }

    [Fact]
    public async Task PostQcReport_EmptyContent_ReturnsError()
    {
        var mockService = new Mock<IQcService>();
        var controller = new QcController(mockService.Object);
        var request = new QcRequest { ReportId = "R001", Findings = "", Impression = "" };

        var result = await controller.PostQcReport(request);
        var objResult = Assert.IsType<OkObjectResult>(result);
        var ajax = Assert.IsType<AjaxResult>(objResult.Value);
        Assert.Equal(400, ajax.Code);
    }

    [Fact]
    public async Task PostQcReport_ValidRequest_DelegatesToService()
    {
        var mockService = new Mock<IQcService>();
        var expectedResult = AjaxResult.Success(new { passed = true });
        mockService.Setup(s => s.ExecuteQcAsync(It.IsAny<QcRequest>()))
            .ReturnsAsync(expectedResult);

        var controller = new QcController(mockService.Object);
        var request = new QcRequest { ReportId = "R001", Findings = "双肺清晰", Impression = "正常" };

        var result = await controller.PostQcReport(request);
        var objResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expectedResult, objResult.Value);

        mockService.Verify(s => s.ExecuteQcAsync(request), Times.Once);
    }
}
