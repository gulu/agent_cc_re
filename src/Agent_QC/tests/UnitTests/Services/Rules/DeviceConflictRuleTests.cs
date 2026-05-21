using Xunit;
using Agent_QC.Models;
using Agent_QC.Services.Rules;

namespace Agent_QC.Tests.UnitTests.Services.Rules;

public class DeviceConflictRuleTests
{
    private readonly DeviceConflictRule _rule = new();

    [Fact]
    public void CT检查出现MRI示_报错()
    {
        var request = new QcRequest { ExamDevice = "CT", Findings = "MRI示右肺结节" };
        var result = _rule.Check(request);
        Assert.Single(result);
    }

    [Fact]
    public void CT检查仅用CT示_不报错()
    {
        var request = new QcRequest { ExamDevice = "CT", Findings = "CT示右肺结节" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void 无设备信息_跳过检查()
    {
        var request = new QcRequest { ExamDevice = null, Findings = "MRI示异常" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }

    [Fact]
    public void MRI检查出现T2WI_不报错()
    {
        var request = new QcRequest { ExamDevice = "MRI", Findings = "T2WI显示异常信号" };
        var result = _rule.Check(request);
        Assert.Empty(result);
    }
}
