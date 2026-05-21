using Microsoft.AspNetCore.Mvc;
using Xunit;
using Agent_QC.Controllers;

namespace Agent_QC.Tests.UnitTests.Controllers;

public class HealthControllerTests
{
    [Fact]
    public void Get_ReturnsOk()
    {
        var controller = new HealthController();
        var result = controller.Get();
        Assert.IsType<OkObjectResult>(result);
    }
}
