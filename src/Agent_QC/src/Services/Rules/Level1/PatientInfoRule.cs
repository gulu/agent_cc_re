using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level1;

/// <summary>
/// 患者基本信息完整性检查——必填字段、性别/年龄有效范围。
/// </summary>
public class PatientInfoRule
{
    public List<QcIssueDto> Check(QcRequest request)
    {
        var issues = new List<QcIssueDto>();

        // 性别有效性
        if (!string.IsNullOrWhiteSpace(request.PatientGender)
            && request.PatientGender != "男" && request.PatientGender != "女")
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "completeness_error",
                SubType = "invalid_field",
                Severity = "error",
                Description = $"患者性别「{request.PatientGender}」不在标准范围内（男/女）",
                Suggestion = "性别应为「男」或「女」",
            });
        }

        // 年龄合理性
        if (request.PatientAge is < 0 or > 150)
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "completeness_error",
                SubType = "invalid_field",
                Severity = "error",
                Description = $"患者年龄 {request.PatientAge} 不在合理范围（0-150）",
                Suggestion = "请核实患者年龄",
            });
        }

        // 检查部位
        if (string.IsNullOrWhiteSpace(request.ExamPart))
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "completeness_error",
                SubType = "missing_required_field",
                Severity = "warning",
                Description = "检查部位不能为空",
                Suggestion = "请填写检查部位",
            });
        }

        // 检查设备
        if (string.IsNullOrWhiteSpace(request.ExamDevice))
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "completeness_error",
                SubType = "missing_required_field",
                Severity = "warning",
                Description = "检查设备不能为空",
                Suggestion = "请填写检查设备",
            });
        }

        return issues;
    }
}
