using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level2;

/// <summary>
/// 解剖命名规范化——非标准解剖术语 → 国际通用解剖命名。
/// </summary>
public class AnatomyTermRule
{
    private static readonly Dictionary<string, string> AnatomyTerms = new()
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
        { "左甲", "甲状腺左叶" }, { "右甲", "甲状腺右叶" },
        { "左侧甲状腺", "甲状腺左叶" }, { "右侧甲状腺", "甲状腺右叶" },
        { "右附件区", "右侧附件区" }, { "左附件区", "左侧附件区" },
        { "头颅", "颅脑" }, { "脑部", "颅脑" },
        { "脑内", "颅内" },
        { "胸腔内", "胸腔" }, { "腹腔内", "腹腔" }, { "盆腔内", "盆腔" },
        { "脊椎", "脊柱" },
        { "颈椎骨", "颈椎" }, { "腰椎骨", "腰椎" },
        { "膝关节内部", "膝关节" }, { "肩关节内部", "肩关节" }, { "髋关节内部", "髋关节" },
        { "甲状腺部位", "甲状腺" }, { "乳腺部位", "乳腺" },
        { "前列腺部位", "前列腺" }, { "前列腺区", "前列腺" },
        { "胆部", "胆囊" }, { "胰部", "胰腺" },
        { "脾部", "脾脏" }, { "胃部", "胃" },
        { "肠部", "肠道" },
        { "血管内部", "血管腔" }, { "血管内", "血管腔" },
        { "管腔内", "管腔" },
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();
        var covered = new HashSet<int>();

        foreach (var (nonStd, standard) in AnatomyTerms.OrderByDescending(k => k.Key.Length))
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
                    IssueType = "terminology_error",
                    SubType = "anatomy_nonstandard",
                    Severity = "warning",
                    OriginalText = nonStd,
                    SuggestedText = standard,
                    Description = $"解剖术语不标准「{nonStd}」，建议使用「{standard}」",
                    Suggestion = $"请使用国际通用解剖命名「{standard}」代替「{nonStd}」",
                });
                break;
            }
        }

        return issues;
    }
}
