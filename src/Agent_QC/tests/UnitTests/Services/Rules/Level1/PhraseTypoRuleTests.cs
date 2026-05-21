using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level1;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level1;

public class PhraseTypoRuleTests
{
    private readonly PhraseTypoRule _rule = new();

    [Fact]
    public void 低密谋灶_报错别字()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "右肺低密谋灶约1cm" };
        var result = _rule.Check(request);
        Assert.Single(result);
        Assert.Equal("text_error", result[0].IssueType);
        Assert.Equal("低密谋灶", result[0].OriginalText);
        Assert.Equal("低密度灶", result[0].SuggestedText);
    }

    [Fact]
    public void 正常描述_不报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "右肺低密度灶约1cm，边界清晰" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 十二脂肠_报错别字()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "十二脂肠溃疡" };
        var result = _rule.Check(request);
        Assert.Single(result);
        Assert.Equal("十二脂肠", result[0].OriginalText);
        Assert.Equal("十二指肠", result[0].SuggestedText);
    }
}
