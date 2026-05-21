using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level2;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level2;

public class ComparisonDescriptionRuleTests
{
    private readonly ComparisonDescriptionRule _rule = new();

    [Fact]
    public void 有比较词但无比较结论_报warning()
    {
        var request = new QcRequest
        {
            ReportId = "R1",
            Findings = "与前片比较，右肺结节。",
        };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "missing_comparison_result");
    }

    [Fact]
    public void 有比较词且有比较结论_不报错()
    {
        var request = new QcRequest
        {
            ReportId = "R1",
            Findings = "与前片比较，结节无明显变化。",
        };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 无比较描述_不报错()
    {
        var request = new QcRequest
        {
            ReportId = "R1",
            Findings = "右肺结节约1cm。",
        };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
