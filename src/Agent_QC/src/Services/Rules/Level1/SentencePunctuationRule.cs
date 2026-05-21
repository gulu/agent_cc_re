using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level1;

/// <summary>
/// 句末标点检查——检查影像所见/诊断结论中的句子是否以合理标点结尾。
/// </summary>
public class SentencePunctuationRule
{
    private static readonly HashSet<char> ValidEndings = new() { '。', '）', '；', '：', '！', '？', '.' };
    private static readonly char[] ListStarters = { '-', '•', '·' };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var issues = new List<QcIssueDto>();

        // 对 Findings 和 Impression 分别检查
        CheckSection(issues, request.Findings ?? "", "findings");
        CheckSection(issues, request.Impression ?? "", "impression");

        return issues;
    }

    private static void CheckSection(List<QcIssueDto> issues, string text, string location)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var sentences = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (trimmed.Length < 4) continue;
            if (ListStarters.Any(c => trimmed.StartsWith(c))) continue;
            if (char.IsDigit(trimmed[0])) continue; // 编号开头的列表项

            if (!ValidEndings.Contains(trimmed[^1]))
            {
                issues.Add(new QcIssueDto
                {
                    IssueType = "text_error",
                    SubType = "missing_period",
                    Severity = "warning",
                    OriginalText = trimmed.Length > 50 ? trimmed[..50] + "..." : trimmed,
                    Description = "句末缺少标点符号（。；：等）",
                    Suggestion = "请在句末添加句号（。）",
                    Location = location,
                });
                break; // 每段落只报告一次，避免重复
            }
        }
    }
}
