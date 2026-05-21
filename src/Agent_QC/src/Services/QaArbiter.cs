using Agent_QC.Models;

namespace Agent_QC.Services;

public class QaArbiter
{
    private const float HighConfidenceThreshold = 0.85f;
    private const float MediumConfidenceThreshold = 0.70f;
    private const float LowConfidenceThreshold = 0.60f;

    /// <summary>Skill ID → IssueType 反向映射</summary>
    private static readonly Dictionary<string, string> SkillToIssue = new()
    {
        ["gender-anatomy-checker"] = "gender_conflict",
        ["site-consistency-checker"] = "direction_conflict",
        ["findings-impression-nli"] = "semantic_conflict",
        ["critical-sign-arbiter"] = "critical_sign",
        ["device-method-validator"] = "device_conflict",
        ["measurement-completeness"] = "completeness_error",
        ["rads-compliance-checker"] = "rads_missing",
        ["terminology-validator"] = "text_error",
    };

    /// <summary>冲突消解：Skill 结果修正规则判定。</summary>
    public Task<List<QcIssueDto>> ArbitrateAsync(
        List<QcIssueDto> ruleIssues, List<SkillResult> skillResults)
    {
        if (skillResults.Count == 0)
            return Task.FromResult(new List<QcIssueDto>(ruleIssues));

        var result = new List<QcIssueDto>();
        var resolvedIssueTypes = new HashSet<string>();

        foreach (var issue in ruleIssues)
        {
            var matchingSkill = FindMatchingSkill(issue, skillResults);
            if (matchingSkill == null)
            {
                // 无 Skill 覆盖 → 保持原判
                result.Add(issue);
                continue;
            }

            resolvedIssueTypes.Add(matchingSkill.SkillId);

            if (matchingSkill.Confidence < LowConfidenceThreshold)
            {
                // 极低置信度 → 忽略 Skill
                result.Add(issue);
            }
            else if (matchingSkill.Judgment == "fail")
            {
                // Skill 确认问题
                issue.Severity = "error";
                issue.Description = $"LLM确认：{issue.Description}（置信度 {matchingSkill.Confidence:P0}）";
                if (!string.IsNullOrEmpty(matchingSkill.Reason))
                    issue.Description += $" — {matchingSkill.Reason}";
                result.Add(issue);
            }
            else if (matchingSkill.Confidence >= HighConfidenceThreshold)
            {
                // 高置信度 pass → 移除（LLM 推翻规则）
            }
            else
            {
                // 中低置信度 pass → 降级为 warning
                issue.Severity = "warning";
                issue.Description = $"LLM不确定：{issue.Description}（置信度 {matchingSkill.Confidence:P0}）";
                result.Add(issue);
            }
        }

        // 添加 Skill 发现但规则未发现的新问题
        foreach (var skill in skillResults)
        {
            if (resolvedIssueTypes.Contains(skill.SkillId)) continue;
            if (skill.Judgment != "fail") continue;
            if (skill.Confidence < MediumConfidenceThreshold) continue;

            var issueType = SkillToIssue.GetValueOrDefault(skill.SkillId, "text_error");
            var severity = skill.Confidence >= HighConfidenceThreshold ? "error" : "warning";

            result.Add(new QcIssueDto
            {
                IssueType = issueType,
                Severity = severity,
                Description = $"LLM发现：{skill.Reason}（置信度 {skill.Confidence:P0}）",
                Suggestion = skill.Suggestion,
            });
        }

        return Task.FromResult(result);
    }

    private static SkillResult? FindMatchingSkill(QcIssueDto issue, List<SkillResult> skills)
    {
        // 通过 IssueType 找到对应的 Skill
        foreach (var skill in skills)
        {
            if (SkillToIssue.TryGetValue(skill.SkillId, out var issueType)
                && issueType == issue.IssueType)
                return skill;
        }
        return null;
    }
}
