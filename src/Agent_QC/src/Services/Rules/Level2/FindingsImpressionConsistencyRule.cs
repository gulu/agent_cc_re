using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level2;

/// <summary>
/// 所见-结论一致性——阴性所见+阳性结论矛盾、程度矛盾检测。
/// </summary>
public class FindingsImpressionConsistencyRule
{
    private static readonly string[] FullNegationPhrases =
    {
        "未见异常", "未见明显异常", "未见明确异常",
        "未见明显器质性异常", "正常", "无异常发现",
        "未见明显病变", "未见确切异常",
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var findings = request.Findings ?? "";
        var impression = request.Impression ?? "";
        var issues = new List<QcIssueDto>();

        if (string.IsNullOrWhiteSpace(findings) || string.IsNullOrWhiteSpace(impression))
            return issues;

        // 所见全阴性 + 结论有阳性关键词 → 矛盾
        var findingsLast = findings.Length > 200 ? findings[^200..] : findings;
        var impressionFirst = impression.Length > 200 ? impression[..200] : impression;

        var findingsAllNegative = FullNegationPhrases.Any(p => findingsLast.Contains(p, StringComparison.Ordinal));
        var impressionAllNegative = FullNegationPhrases.Any(p => impressionFirst.Contains(p, StringComparison.Ordinal));

        // 仅当所见阴性 + 结论非阴性时报警（避免"未见异常→未见异常"误报）
        if (findingsAllNegative && !impressionAllNegative)
        {
            var positiveWords = new[] { "结节", "肿块", "占位", "病变", "骨折", "出血", "炎症", "感染", "异常" };
            if (positiveWords.Any(w => impressionFirst.Contains(w, StringComparison.Ordinal)))
            {
                issues.Add(new QcIssueDto
                {
                    IssueType = "semantic_conflict",
                    SubType = "diagnosis_jump",
                    Severity = "error",
                    Description = "所见描述均为阴性（正常），但结论给出阳性诊断",
                    Suggestion = "请检查所见描述是否遗漏了阳性发现",
                });
            }
        }

        // 程度矛盾：所见轻度 + 结论重度
        var severityPairs = new (string mild, string severe)[]
        {
            ("轻度", "重度"), ("轻度", "严重"), ("轻微", "重度"),
            ("少量", "大量"), ("少许", "大量"),
            ("不明显", "明显"), ("不明显", "显著"),
            ("轻度", "广泛"), ("局限", "弥漫"),
        };
        foreach (var (mild, severe) in severityPairs)
        {
            if (findingsLast.Contains(mild, StringComparison.Ordinal)
                && impressionFirst.Contains(severe, StringComparison.Ordinal))
            {
                issues.Add(new QcIssueDto
                {
                    IssueType = "semantic_conflict",
                    SubType = "severity_conflict",
                    Severity = "error",
                    Description = $"所见描述为「{mild}」但结论描述为「{severe}」，程度矛盾",
                    Suggestion = "请确认所见与结论中的程度描述是否一致",
                });
                break;
            }
        }

        return issues;
    }
}
