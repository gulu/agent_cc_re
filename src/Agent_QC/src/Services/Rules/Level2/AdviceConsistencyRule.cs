using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level2;

/// <summary>
/// 建议-诊断一致性——可疑诊断需随访建议，良性诊断不应有过度检查建议。
/// </summary>
public class AdviceConsistencyRule
{
    public List<QcIssueDto> Check(QcRequest request)
    {
        var impression = request.Impression ?? "";
        var issues = new List<QcIssueDto>();

        // 有可疑诊断但缺少随访建议
        var suspiciousWords = new[] { "考虑", "可疑", "不除外", "恶性", "占位", "建议活检", "性质待定" };
        var followupWords = new[] { "建议随访", "建议复查", "建议进一步", "随访", "复查", "定期检查" };

        var hasSuspicious = suspiciousWords.Any(w => impression.Contains(w, StringComparison.Ordinal));
        var hasFollowup = followupWords.Any(w => impression.Contains(w, StringComparison.Ordinal));

        if (hasSuspicious && !hasFollowup)
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "terminology_error",
                SubType = "missing_followup",
                Severity = "warning",
                Description = "诊断结论涉及可疑/占位性病变，但缺少随访或进一步检查建议",
                Suggestion = "请根据诊断结论补充随访建议或进一步检查方案",
            });
        }

        // 良性但有过度检查建议
        var benignWords = new[] { "良性", "正常", "未见异常", "未见明确异常" };
        var overCheckWords = new[] { "建议活检", "建议穿刺", "建议手术" };

        var hasBenign = benignWords.Any(w => impression.Contains(w, StringComparison.Ordinal));
        var hasOverCheck = overCheckWords.Any(w => impression.Contains(w, StringComparison.Ordinal));

        if (hasBenign && hasOverCheck)
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "terminology_error",
                SubType = "over_check_advice",
                Severity = "warning",
                Description = "诊断结论为良性/正常，但建议中包含了活检/穿刺/手术等过度检查措施",
                Suggestion = "请确认诊断结论与建议是否匹配",
            });
        }

        return issues;
    }
}
