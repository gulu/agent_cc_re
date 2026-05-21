using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level2;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level2;

public class RadsClassificationRuleTests
{
    private readonly RadsClassificationRule _rule = new();

    [Fact]
    public void 乳腺报告缺BI_RADS_报error()
    {
        var request = new QcRequest { ReportId = "R1", ReportType = "乳腺钼靶", Impression = "乳腺结节，建议复查。" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.IssueType == "rads_missing");
    }

    [Fact]
    public void 乳腺报告有BI_RADS_不报错()
    {
        var request = new QcRequest { ReportId = "R1", ReportType = "乳腺超声", Impression = "BI-RADS 3类，建议复查。" };
        var result = _rule.Check(request);
        Assert.DoesNotContain(result, i => i.IssueType == "rads_missing");
    }

    [Fact]
    public void 无匹配类型_跳过检查()
    {
        var request = new QcRequest { ReportId = "R1", ReportType = "头颅CT", Impression = "未见异常。" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
