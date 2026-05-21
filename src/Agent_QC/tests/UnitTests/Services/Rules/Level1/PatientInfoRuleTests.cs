using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules.Level1;

namespace Agent_QC.Tests.UnitTests.Services.Rules.Level1;

public class PatientInfoRuleTests
{
    private readonly PatientInfoRule _rule = new();

    [Fact]
    public void 性别不在男女范围_报error()
    {
        var request = new QcRequest { ReportId = "R1", PatientGender = "未知", PatientAge = 30, ExamPart = "胸部", ExamDevice = "CT" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "invalid_field");
    }

    [Fact]
    public void 年龄超出范围_报error()
    {
        var request = new QcRequest { ReportId = "R1", PatientGender = "男", PatientAge = 200, ExamPart = "胸部", ExamDevice = "CT" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "invalid_field");
    }

    [Fact]
    public void 信息完整_不报错()
    {
        var request = new QcRequest { ReportId = "R1", PatientGender = "男", PatientAge = 30, ExamPart = "胸部", ExamDevice = "CT" };
        var result = _rule.Check(request);
        Assert.DoesNotContain(result, i => i.SubType == "invalid_field");
    }

    [Fact]
    public void 检查部位为空_报warning()
    {
        var request = new QcRequest { ReportId = "R1", PatientGender = "女", PatientAge = 30, ExamPart = "", ExamDevice = "CT" };
        var result = _rule.Check(request);
        Assert.Contains(result, i => i.SubType == "missing_required_field");
    }
}
