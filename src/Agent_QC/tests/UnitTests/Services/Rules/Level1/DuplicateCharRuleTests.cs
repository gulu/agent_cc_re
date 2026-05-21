using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level1;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level1;

public class DuplicateCharRuleTests
{
    private readonly DuplicateCharRule _rule = new();

    [Fact]
    public void 重复字_报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "右肺见结节节影" };
        var result = _rule.Check(request);
        Assert.Single(result);
        Assert.Equal("duplicate_char", result[0].SubType);
    }

    [Fact]
    public void 合法叠字_慢慢_不报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "病灶慢慢增大" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 正常文本_不报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "右肺结节约1cm，边界清晰" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
