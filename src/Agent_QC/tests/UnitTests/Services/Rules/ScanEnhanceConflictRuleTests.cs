using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules;

namespace Agent_QC.Tests.UnitTests.Services.Rules;

public class ScanEnhanceConflictRuleTests
{
    private readonly ScanEnhanceConflictRule _rule = new();

    [Fact]
    public void 平扫出现强化_报错()
    {
        var request = new QcRequest { ExamMethod = "平扫", Findings = "明显强化" };
        var result = _rule.Check(request);
        Assert.Single(result);
    }

    [Fact]
    public void 增强扫描出现强化_不报错()
    {
        var request = new QcRequest { ExamMethod = "增强", Findings = "明显强化" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 无扫描方式_跳过检查()
    {
        var request = new QcRequest { ExamMethod = null, Findings = "明显强化" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
