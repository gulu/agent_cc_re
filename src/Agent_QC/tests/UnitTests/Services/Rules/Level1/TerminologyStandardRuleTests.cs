using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level1;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level1;

public class TerminologyStandardRuleTests
{
    private readonly TerminologyStandardRule _rule = new();

    [Fact]
    public void 右上肺_不规范_报warning()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "右上肺结节" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.OriginalText == "右上肺" && i.SuggestedText == "右肺上叶");
    }

    [Fact]
    public void 标准术语_不报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "右肺上叶结节" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 头颅_不规范_报warning()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "头颅CT平扫" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.OriginalText == "头颅" && i.SuggestedText == "颅脑");
    }
}
