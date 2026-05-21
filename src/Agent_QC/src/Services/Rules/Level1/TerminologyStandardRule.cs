using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level1;

/// <summary>
/// 术语标准查表——非标准术语 → 标准术语映射。
/// 数据来源：knowledge/terminology.yaml（常用条目内嵌）。
/// </summary>
public class TerminologyStandardRule
{
    private static readonly Dictionary<string, string> StandardTerms = new()
    {
        { "右上肺", "右肺上叶" }, { "右下肺", "右肺下叶" },
        { "右肺上部", "右肺上叶" }, { "右肺下部", "右肺下叶" },
        { "右中肺", "右肺中叶" }, { "右肺中部", "右肺中叶" },
        { "左上肺", "左肺上叶" }, { "左下肺", "左肺下叶" },
        { "左肺上部", "左肺上叶" }, { "左肺下部", "左肺下叶" },
        { "右上叶", "右肺上叶" }, { "右下叶", "右肺下叶" },
        { "左上叶", "左肺上叶" }, { "左下叶", "左肺下叶" },
        { "肝右", "肝右叶" }, { "肝左", "肝左叶" },
        { "右肝", "肝右叶" }, { "左肝", "肝左叶" },
        { "右半肝", "肝右叶" }, { "左半肝", "肝左叶" },
        { "肝脏右叶", "肝右叶" }, { "肝脏左叶", "肝左叶" },
        { "甲状腺左", "甲状腺左叶" }, { "甲状腺右", "甲状腺右叶" },
        { "左侧甲状腺", "甲状腺左叶" }, { "右侧甲状腺", "甲状腺右叶" },
        { "头颅", "颅脑" }, { "脑部", "颅脑" },
        { "脑内", "颅内" },
        { "脊椎", "脊柱" },
        { "颈椎骨", "颈椎" }, { "腰椎骨", "腰椎" },
        { "胆部", "胆囊" }, { "胰部", "胰腺" },
        { "脾部", "脾脏" },
        { "血管内部", "血管腔" }, { "血管内", "血管腔" },
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();
        var covered = new HashSet<int>();

        // 最长优先避免子串重复匹配
        foreach (var (nonStd, standard) in StandardTerms.OrderByDescending(k => k.Key.Length))
        {
            var idx = 0;
            while ((idx = fullText.IndexOf(nonStd, idx, StringComparison.Ordinal)) >= 0)
            {
                var end = idx + nonStd.Length;
                if (Enumerable.Range(idx, nonStd.Length).Any(covered.Contains))
                {
                    idx++;
                    continue;
                }
                for (int i = idx; i < end; i++) covered.Add(i);

                issues.Add(new QcIssueDto
                {
                    IssueType = "terminology_nonstandard",
                    Severity = "warning",
                    OriginalText = nonStd,
                    SuggestedText = standard,
                    Description = $"「{nonStd}」应为规范术语「{standard}」",
                    Suggestion = $"建议使用「{standard}」代替「{nonStd}」",
                });
                break;
            }
        }

        return issues;
    }
}
