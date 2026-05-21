using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level1;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level1;

public class UnitFormatRuleTests
{
    private readonly UnitFormatRule _rule = new();

    [Fact]
    public void 乘法符号x不规范_报warning()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "结节大小1.5 x 2.0cm" };
        var result = _rule.Check(request);
        Assert.Single(result);
        Assert.Equal("multiply_sign", result[0].SubType);
    }

    [Fact]
    public void 无尺寸描述_不报错()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "右肺未见异常。" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 相邻尺寸单位不一致_cm和mm_报warning()
    {
        var request = new QcRequest { ReportId = "R1", Findings = "结节大小约1.5cm，周围见3mm小结节" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "unit_mismatch");
    }
}
