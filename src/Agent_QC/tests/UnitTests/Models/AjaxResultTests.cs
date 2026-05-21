using Xunit;
using Agent_QC.Models;

namespace Agent_QC.Tests.UnitTests.Models;

public class AjaxResultTests
{
    [Fact]
    public void Success_ReturnsCode200()
    {
        var result = AjaxResult.Success(new { msg = "ok" });
        Assert.Equal(200, result.Code);
    }

    [Fact]
    public void Success_ContainsData()
    {
        var data = new { foo = "bar" };
        var result = AjaxResult.Success(data);
        Assert.Same(data, result.Data);
    }

    [Fact]
    public void Success_DefaultMsg()
    {
        var result = AjaxResult.Success(null);
        Assert.Equal("success", result.Msg);
    }

    [Fact]
    public void Error_ReturnsCode500()
    {
        var result = AjaxResult.Error("出错了");
        Assert.Equal(500, result.Code);
        Assert.Equal("出错了", result.Msg);
    }

    [Fact]
    public void Error_WithCustomCode()
    {
        var result = AjaxResult.Error(400, "参数错误");
        Assert.Equal(400, result.Code);
        Assert.Equal("参数错误", result.Msg);
    }
}
