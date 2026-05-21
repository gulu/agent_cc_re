using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules;

namespace Agent_QC.Tests.UnitTests.Services.Rules;

public class DirectionConflictRuleTests
{
    private readonly DirectionConflictRule _rule = new();

    [Fact]
    public void 影像所见左侧_诊断结论右侧_报warning()
    {
        var request = new QcRequest { Findings = "左侧", Impression = "右侧" };
        var result = _rule.Check(request);
        Assert.Single(result);
    }

    [Fact]
    public void 影像所见和诊断结论同为左侧_不报错()
    {
        var request = new QcRequest { Findings = "左侧", Impression = "左侧" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 无方向词_不报错()
    {
        var request = new QcRequest { Findings = "正常", Impression = "正常" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 仅Findings有方向_不报错()
    {
        var request = new QcRequest { Findings = "左侧", Impression = "未见异常" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
