using Agent_QC.Models;

namespace Agent_QC.Services.Rules;

/// <summary>
/// 性别-解剖部位矛盾检测。
/// 检查患者性别与报告中出现的解剖部位是否矛盾（如男性出现子宫、女性出现前列腺）。
/// </summary>
public class GenderConflictRule
{
    private readonly NegationDetector _negationDetector = new();

    /// <summary>女性专有部位（男性患者报告中不应出现）。</summary>
    private static readonly string[] FemaleParts =
    {
        "子宫", "宫颈", "卵巢", "输卵管", "阴道", "子宫内膜", "附件", "乳腺", "乳房",
        "宫腔", "宫颈管", "卵泡", "黄体", "子宫肌瘤", "卵巢囊肿", "输卵管积水",
    };

    /// <summary>男性专有部位（女性患者报告中不应出现）。</summary>
    private static readonly string[] MaleParts =
    {
        "前列腺", "精囊", "睾丸", "附睾", "阴囊", "阴茎", "精索", "输精管",
        "前列腺增生", "睾丸鞘膜积液",
    };

    /// <summary>排除模式：术后/切除后复查等场景。</summary>
    private static readonly string[] ExcludePatterns =
    {
        "切除术后", "术后复查", "术后改变", "摘除术后",
        "未见显示", "未见明确",
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PatientGender))
            return new List<QcIssueDto>();

        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var isMale = request.PatientGender == "男";
        var keywords = isMale ? FemaleParts : MaleParts;
        var oppositeGender = isMale ? "女性" : "男性";

        // 按长度降序排列，避免短关键词被长关键词重复匹配
        // 如"子宫肌瘤"包含"子宫"，先匹配长的避免重复
        keywords = keywords.OrderByDescending(k => k.Length).ToArray();

        var issues = new List<QcIssueDto>();
        var coveredRanges = new HashSet<int>(); // 已处理的位置范围，防止重复

        foreach (var keyword in keywords)
        {
            int idx = 0;
            while ((idx = fullText.IndexOf(keyword, idx, StringComparison.Ordinal)) != -1)
            {
                int end = idx + keyword.Length;

                // 跳过已被更长关键词覆盖的位置
                if (coveredRanges.Any(r => r >= idx && r < end))
                {
                    idx = end;
                    continue;
                }

                // 否定语境跳过：如"未见子宫"
                if (_negationDetector.IsNegated(fullText, keyword)) { idx = end; continue; }

                // 术后等排除模式跳过：如"子宫切除术后"
                if (HasExcludePattern(fullText, keyword, idx)) { idx = end; continue; }

                // 标记位置范围
                for (int i = idx; i < end; i++) coveredRanges.Add(i);

                issues.Add(new QcIssueDto
                {
                    IssueType = "gender_conflict",
                    Severity = "critical",
                    Description = $"患者性别为{request.PatientGender}，但报告中出现{oppositeGender}部位「{keyword}」",
                });

                idx = end;
            }
        }

        return issues;
    }

    /// <summary>检查关键词所在位置附近是否有排除模式（如"切除术后"）。</summary>
    private static bool HasExcludePattern(string text, string keyword, int kwIdx)
    {
        int start = Math.Max(0, kwIdx - 5);
        int end = Math.Min(text.Length, kwIdx + keyword.Length + 10);
        var context = text[start..end];

        return ExcludePatterns.Any(p => context.Contains(p, StringComparison.Ordinal));
    }
}
