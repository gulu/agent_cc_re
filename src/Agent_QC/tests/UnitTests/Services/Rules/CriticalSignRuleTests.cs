using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules;

namespace Agent_QC.Tests.UnitTests.Services.Rules;

public class CriticalSignRuleTests
{
    private readonly CriticalSignRule _rule = new();

    [Fact]
    public void 报告含主动脉夹层_报critical()
    {
        var request = new QcRequest { Findings = "主动脉夹层" };
        var result = _rule.Check(request);
        Assert.Single(result);
        Assert.Equal("critical", result[0].Severity);
    }

    [Fact]
    public void 未见主动脉夹层_不报错()
    {
        var request = new QcRequest { Findings = "未见明确主动脉夹层征象" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 正常报告_不报错()
    {
        var request = new QcRequest { Findings = "双肺纹理清晰，未见异常" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 脑出血_报critical()
    {
        var request = new QcRequest { Impression = "脑出血" };
        var result = _rule.Check(request);
        Assert.Single(result);
    }
}
