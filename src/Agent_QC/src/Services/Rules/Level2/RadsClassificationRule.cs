using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level2;

/// <summary>
/// RADS 分级检查——按报告类型检查对应 RADS 分级是否在结论中出现。
/// </summary>
public class RadsClassificationRule
{
    private static readonly Dictionary<string, string> RadsTypeMap = new()
    {
        { "乳腺", "BI-RADS" }, { "钼靶", "BI-RADS" }, { "乳腺超声", "BI-RADS" },
        { "肝脏", "LI-RADS" }, { "肝脏MRI", "LI-RADS" },
        { "甲状腺", "TI-RADS" }, { "甲状腺超声", "TI-RADS" },
        { "前列腺", "PI-RADS" }, { "前列腺MRI", "PI-RADS" },
        { "肺部", "Lung-RADS" }, { "肺结节", "Lung-RADS" }, { "胸部CT筛查", "Lung-RADS" },
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var impression = request.Impression ?? "";
        var reportType = request.ReportType ?? "";
        var issues = new List<QcIssueDto>();

        foreach (var (typeKeyword, radsName) in RadsTypeMap)
        {
            if (!reportType.Contains(typeKeyword, StringComparison.Ordinal)) continue;
            if (impression.Contains(radsName, StringComparison.OrdinalIgnoreCase)) continue;

            issues.Add(new QcIssueDto
            {
                IssueType = "rads_missing",
                Severity = "error",
                Description = $"{reportType} 报告应在结论中标注 {radsName} 分级",
                Suggestion = $"请在诊断结论中补充 {radsName} 分级",
            });
            break;
        }

        return issues;
    }
}
