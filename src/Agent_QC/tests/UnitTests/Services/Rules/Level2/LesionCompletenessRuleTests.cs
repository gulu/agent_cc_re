using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level2;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level2;

public class LesionCompletenessRuleTests
{
    private readonly LesionCompletenessRule _rule = new();

    [Fact]
    public void 有结节但无尺寸_报error()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "右肺见结节影，边界清晰。" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "missing_size");
    }

    [Fact]
    public void 无病灶_不报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "胸部未见异常。" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 多发未标注具体数量_报warning()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "双肺多发性结节，较大者约0.5cm" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "missing_count");
    }
}
