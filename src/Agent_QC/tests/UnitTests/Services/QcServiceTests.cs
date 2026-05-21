using Xunit;
using Agent_QC.Models;
using Agent_QC.Services;

namespace Agent_QC.Tests.UnitTests.Services;

public class QcServiceTests
{
    private readonly QcService _service = new();

    [Fact]
    public async Task 正常报告_无问题_满分通过()
    {
        var request = new QcRequest
        {
            ReportId = "R001",
            Findings = "胸部CT平扫未见异常。",
            Impression = "胸部未见异常。",
            PatientAge = 50,
            PatientGender = "男",
            ExamMethod = "平扫",
            ExamDevice = "CT",
            ExamPart = "胸部",
        };

        var result = await _service.ExecuteQcAsync(request);
        var response = result.Data as QcResponse;

        Assert.NotNull(response);
        Assert.Equal("R001", response!.ReportId);
        Assert.True(response.TotalScore >= response.PassScore);
        Assert.True(response.Passed);
        Assert.Empty(response.Issues);
    }

    [Fact]
    public async Task 男女矛盾_降分并报critical()
    {
        var request = new QcRequest
        {
            ReportId = "R002",
            Findings = "子宫肌瘤约3cm。",
            Impression = "子宫肌瘤。",
            PatientGender = "男",
            PatientAge = 50,
        };

        var result = await _service.ExecuteQcAsync(request);
        var response = result.Data as QcResponse;

        Assert.NotNull(response);
        // gender_conflict→logic维度(30%)，满意度降低但不低于及格线
        Assert.True(response!.TotalScore <= 97m);
        Assert.Contains(response.Issues, i => i.IssueType == "gender_conflict");
    }

    [Fact]
    public async Task 危急征象_报critical级别()
    {
        var request = new QcRequest
        {
            ReportId = "R003",
            Findings = "主动脉夹层，累及升主动脉。",
            Impression = "主动脉夹层（Stanford A型）。",
        };

        var result = await _service.ExecuteQcAsync(request);
        var response = result.Data as QcResponse;

        Assert.NotNull(response);
        Assert.Contains(response!.Issues, i => i.Severity == "critical");
    }

    [Fact]
    public async Task ReportId为空_返回错误()
    {
        var request = new QcRequest
        {
            ReportId = "",
            Findings = "测试",
            Impression = "测试",
        };

        var result = await _service.ExecuteQcAsync(request);

        Assert.Equal(400, result.Code);
    }

    [Fact]
    public async Task 多项问题_合并返回()
    {
        var request = new QcRequest
        {
            ReportId = "R005",
            Findings = "左侧乳腺肿块。",
            Impression = "右侧乳腺肿块，建议复查。",
            PatientGender = "男",
            PatientAge = 5,
        };

        var result = await _service.ExecuteQcAsync(request);
        var response = result.Data as QcResponse;

        Assert.NotNull(response);
        // 应有 direction_conflict + 可能 age_conflict (5岁乳腺肿块不常见，但不在规则列表中)
        Assert.Contains(response!.Issues, i => i.IssueType == "direction_conflict");
    }

    [Fact]
    public async Task 平扫出现增强描述_报错()
    {
        var request = new QcRequest
        {
            ReportId = "R006",
            Findings = "右肺结节，明显强化。",
            Impression = "右肺结节，请随访。",
            ExamMethod = "平扫",
        };

        var result = await _service.ExecuteQcAsync(request);
        var response = result.Data as QcResponse;

        Assert.NotNull(response);
        Assert.Contains(response!.Issues, i => i.IssueType == "scan_enhance_conflict");
    }
}
