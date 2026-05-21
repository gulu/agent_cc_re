using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level2;

/// <summary>
/// 口语化/非客观描述检测——报告应使用客观、精确的医学术语。
/// </summary>
public class ColloquialTermRule
{
    private static readonly (string word, string suggestion)[] ColloquialTerms =
    {
        ("有点", "建议使用精确描述"),
        ("还好", "建议使用精确描述"),
        ("看起来", "建议使用客观描述"),
        ("似乎", "建议使用确定语气"),
        ("好像", "建议使用确定语气"),
        ("不太", "建议使用明确描述"),
        ("比较大", "建议标注具体尺寸"),
        ("比较小", "建议标注具体尺寸"),
        ("若干个", "建议标注具体数量"),
        ("好几个", "建议标注具体数量"),
        ("基本上", "建议使用精确描述"),
        ("差不多", "建议使用精确描述"),
        ("大概", "建议标注具体数值"),
        ("也许", "建议使用确定语气"),
        ("估计", "建议使用确定语气"),
        ("看不清楚", "建议明确描述为「显示欠清」"),
        ("看不太清", "建议使用「显示欠清」"),
        ("隐约可见", "建议使用「隐约显示」"),
        ("一般般", "建议使用精确描述"),
        ("一大片", "建议使用精确尺寸"),
        ("一点点", "建议使用精确尺寸"),
        ("有些", "建议使用明确描述"),
        ("比较", "建议使用比较级精确描述"),
        ("这个", "建议使用医学名词代替指示代词"),
        ("那个", "建议使用医学名词代替指示代词"),
        ("这边", "建议使用解剖方位"),
        ("那边", "建议使用解剖方位"),
        ("还行", "建议使用精确描述"),
        ("挺", "建议使用精确程度词"),
        ("非常", "建议使用精确程度词"),
        ("特别", "建议使用精确描述"),
        ("很", "建议使用量化描述"),
        ("不少", "建议标注具体数量"),
        ("一部分", "建议标注具体描述"),
        ("两边", "建议使用「双侧」"),
        ("上面", "建议使用「上方」或具体解剖方位"),
        ("下面", "建议使用「下方」或具体解剖方位"),
        ("旁边", "建议使用具体解剖方位"),
        ("周围一圈", "建议使用「环绕」或「周围」"),
        ("中间", "建议使用「中央」或「中心」"),
        ("越来越", "建议使用「进行性」"),
        ("总算", "非医学用语，建议删除"),
        ("目前看来", "建议使用「目前」"),
        ("大体上", "建议使用「大体」"),
        ("不怎么", "建议使用明确描述"),
        ("感觉", "非客观用语，建议使用「显示/提示」"),
        ("看上去", "建议使用「显示」"),
        ("可见到", "建议使用「可见」（冗余表达）"),
        ("可以见到", "建议使用「可见」"),
        ("我们认为", "建议删除（报告中不应出现主观表述）"),
        ("据观察", "建议删除"),
        ("应该说", "非医学用语"),
        ("相当", "建议使用明确的量化描述"),
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();
        var covered = new HashSet<int>();

        // 最长优先避免子串重复匹配（如"看不清楚"包含"清楚"等情况）
        foreach (var (word, suggestion) in ColloquialTerms.OrderByDescending(k => k.word.Length))
        {
            var idx = 0;
            while ((idx = fullText.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                var end = idx + word.Length;
                if (Enumerable.Range(idx, word.Length).Any(covered.Contains))
                {
                    idx++;
                    continue;
                }
                for (int i = idx; i < end; i++) covered.Add(i);

                issues.Add(new QcIssueDto
                {
                    IssueType = "terminology_error",
                    SubType = "colloquial",
                    Severity = "warning",
                    OriginalText = word,
                    Description = $"报告中出现口语化/非客观描述「{word}」",
                    Suggestion = suggestion,
                });
                break;
            }
        }

        return issues;
    }
}
