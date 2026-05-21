using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level2;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level2;

public class ColloquialTermRuleTests
{
    private readonly ColloquialTermRule _rule = new();

    [Fact]
    public void 口语化_看起来_报warning()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "看起来右肺有结节" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.OriginalText == "看起来");
    }

    [Fact]
    public void 口语化_两边_报warning()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "两边肺纹理增多" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.OriginalText == "两边");
    }

    [Fact]
    public void 规范用语_不报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "双侧肺纹理增多，建议随访。" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
