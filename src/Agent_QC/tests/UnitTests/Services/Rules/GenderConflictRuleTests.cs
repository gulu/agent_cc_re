using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules;

namespace Agent_QC.Tests.UnitTests.Services.Rules;

public class GenderConflictRuleTests
{
    private readonly GenderConflictRule _rule = new();

    [Fact]
    public void 男性患者出现子宫_报critical()
    {
        var request = new QcRequest { PatientGender = "男", Findings = "子宫肌瘤" };
        var result = _rule.Check(request);
        Assert.Single(result);
        Assert.Equal("critical", result[0].Severity);
        Assert.Contains("子宫", result[0].Description);
    }

    [Fact]
    public void 女性患者出现前列腺_报critical()
    {
        var request = new QcRequest { PatientGender = "女", Findings = "前列腺增生" };
        var result = _rule.Check(request);
        Assert.Single(result);
        Assert.Contains("前列腺", result[0].Description);
    }

    [Fact]
    public void 男性患者_未见子宫_不报错()
    {
        var request = new QcRequest { PatientGender = "男", Findings = "未见子宫及附件异常" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 男性患者_子宫切除术后_不报错()
    {
        var request = new QcRequest { PatientGender = "男", Findings = "子宫切除术后复查" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 男性患者无女性器官_不报错()
    {
        var request = new QcRequest { PatientGender = "男", Findings = "双肺清晰，肝肾功能正常" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 无性别信息_跳过检查()
    {
        var request = new QcRequest { PatientGender = null, Findings = "子宫肌瘤" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
