using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules;

namespace Agent_QC.Tests.UnitTests.Services.Rules;

public class AgeConflictRuleTests
{
    private readonly AgeConflictRule _rule = new();

    [Fact]
    public void 十岁出现骨质疏松_报warning()
    {
        var request = new QcRequest { PatientAge = 10, Findings = "骨质疏松" };
        var result = _rule.Check(request);
        Assert.Single(result);
    }

    [Fact]
    public void 七十岁骨质疏松_不报错()
    {
        var request = new QcRequest { PatientAge = 70, Findings = "骨质疏松" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 无年龄信息_跳过检查()
    {
        var request = new QcRequest { PatientAge = null, Findings = "骨质疏松" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 小儿肝硬化_报错()
    {
        var request = new QcRequest { PatientAge = 5, Findings = "肝硬化" };
        var result = _rule.Check(request);
        Assert.Single(result);
    }
}
