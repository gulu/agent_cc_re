using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level2;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level2;

public class AdviceConsistencyRuleTests
{
    private readonly AdviceConsistencyRule _rule = new();

    [Fact]
    public void 可疑诊断缺随访建议_报warning()
    {
        var request = new QcRequest
        {
            ReportId = "R1",
            Impression = "右肺结节，恶性不除外。",
        };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "missing_followup");
    }

    [Fact]
    public void 良性但有过度检查建议_报warning()
    {
        var request = new QcRequest
        {
            ReportId = "R1",
            Impression = "未见明确异常，建议穿刺活检。",
        };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "over_check_advice");
    }

    [Fact]
    public void 正常报告_不报错()
    {
        var request = new QcRequest
        {
            ReportId = "R1",
            Impression = "胸部未见异常。",
        };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
