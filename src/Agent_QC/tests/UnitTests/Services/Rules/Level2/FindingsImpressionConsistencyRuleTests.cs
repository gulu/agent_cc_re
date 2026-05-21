using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level2;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level2;

public class FindingsImpressionConsistencyRuleTests
{
    private readonly FindingsImpressionConsistencyRule _rule = new();

    [Fact]
    public void 所见阴性结论阳性_报error()
    {
        var request = new QcRequest
        {
            ReportId = "R1",
            Findings = "胸部CT平扫未见异常。",
            Impression = "右肺结节，建议复查。",
        };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "diagnosis_jump");
    }

    [Fact]
    public void 所见轻度结论重度_报error()
    {
        var request = new QcRequest
        {
            ReportId = "R1",
            Findings = "双肺轻度炎症。",
            Impression = "双肺重度感染。",
        };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "severity_conflict");
    }

    [Fact]
    public void 一致描述_不报错()
    {
        var request = new QcRequest
        {
            ReportId = "R1",
            Findings = "右肺见结节约1cm，边界清晰。",
            Impression = "右肺结节，建议随访。",
        };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
