using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level2;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level2;

public class AnatomyTermRuleTests
{
    private readonly AnatomyTermRule _rule = new();

    [Fact]
    public void 右上肺_不规范_报warning()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "右上肺结节" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.OriginalText == "右上肺" && i.SuggestedText == "右肺上叶");
    }

    [Fact]
    public void 标准解剖命名_不报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "右肺上叶结节" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 胸腔内_不规范_报warning()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "胸腔内见积液" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.OriginalText == "胸腔内" && i.SuggestedText == "胸腔");
    }
}
