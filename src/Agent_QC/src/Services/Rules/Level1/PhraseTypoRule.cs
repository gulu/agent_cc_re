using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level1;

/// <summary>
/// 医学词组错别字检测。形近/音近词组上下文感知匹配。
/// </summary>
public class PhraseTypoRule
{
    /// <summary>错字词组 → 正确词组（仅报告中出现这些词组时才报错）</summary>
    private static readonly Dictionary<string, string> PhraseTypos = new()
    {
        { "低密谋灶", "低密度灶" },
        { "高密谋灶", "高密度灶" },
        { "轨道症", "轨道征" },
        { "毛刺症", "毛刺征" },
        { "分页症", "分叶征" },
        { "胸膜凹陷症", "胸膜凹陷征" },
        { "空泡症", "空泡征" },
        { "钙化点", "钙化灶" },
        { "低密灶", "低密度灶" },
        { "影像学证", "影像学征" },
        { "十二脂肠", "十二指肠" },
        { "喷门", "贲门" },
        { "综合隔", "纵隔" },
        { "肾孟", "肾盂" },
        { "输柰管", "输尿管" },
        { "胆琅", "胆囊" },
        { "骨拆", "骨折" },
        { "破列", "破裂" },
        { "坏列", "坏死" },
        { "积夜", "积液" },
        { "肺大泡", "肺大疱" },
        { "颅内血钟", "颅内血肿" },
        { "门静", "门静脉" },
        { "下腔静", "下腔静脉" },
        { "随质", "髓质" },
        { "骨皮贡", "骨皮质" },
        { "尿路洁石", "尿路结石" },
        { "洁石", "结石" },
        { "密谋", "密度" },
        { "谋灶", "密灶" },
        { "症性", "征性" },
        { "性症", "性征" },
        { "痞脏", "脾脏" },
        { "干脏", "肝脏" },
        { "贤脏", "肾脏" },
        { "费部", "肺部" },
        { "杆脏", "肝脏" },
        { "干硬化", "肝硬化" },
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();
        var covered = new HashSet<int>();

        // 最长优先避免子串重复匹配（如"低密谋灶"不要同时匹配"密谋""谋灶"）
        foreach (var (wrong, correct) in PhraseTypos.OrderByDescending(k => k.Key.Length))
        {
            var idx = 0;
            while ((idx = fullText.IndexOf(wrong, idx, StringComparison.Ordinal)) >= 0)
            {
                var end = idx + wrong.Length;
                // 跳过已匹配的字符范围
                if (Enumerable.Range(idx, wrong.Length).Any(covered.Contains))
                {
                    idx++;
                    continue;
                }
                for (int i = idx; i < end; i++) covered.Add(i);

                issues.Add(new QcIssueDto
                {
                    IssueType = "text_error",
                    SubType = "phrase_typo",
                    Severity = "error",
                    OriginalText = wrong,
                    SuggestedText = correct,
                    Description = $"疑似错别字「{wrong}」，应为「{correct}」",
                    Suggestion = $"请将「{wrong}」更正为「{correct}」",
                });
                break;
            }
        }

        return issues;
    }
}
