using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level1;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level1;

public class SentencePunctuationRuleTests
{
    private readonly SentencePunctuationRule _rule = new();

    [Fact]
    public void 句子缺少句号_报warning()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "影像所见：右肺结节约1cm大小，边界清晰" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "missing_period");
    }

    [Fact]
    public void 句子以句号结尾_不报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "影像所见：右肺结节约1cm大小，边界清晰。" };
        var result = _rule.Check(request);
        Assert.DoesNotContain(result, i => i.SubType == "missing_period");
    }

    [Fact]
    public void 短句跳过_不报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "正常。" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
