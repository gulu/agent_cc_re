using System.Text.RegularExpressions;
using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level1;

/// <summary>
/// 重复字检测——正则匹配连续相同汉字，白名单排除合法叠字。
/// </summary>
public partial class DuplicateCharRule
{
    [GeneratedRegex(@"([\p{IsCJKUnifiedIdeographs}])\1{1,}", RegexOptions.Compiled)]
    private static partial Regex DupPattern();

    /// <summary>合法叠字白名单（医学报告中常见的重叠用法）</summary>
    private static readonly HashSet<char> ValidDuplicates = new()
    {
        '慢', '常', '渐', '隐', '显', '明', '微', '轻', '重', '淡', '浓',
        '大', '小', '高', '低', '多', '少', '远', '近', '前', '后', '上', '下',
        '层', '次', '粒', '点', '斑',
        '薄', '厚', '深', '浅', '均', '略', '稍', '极', '最', '更',
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();

        foreach (Match m in DupPattern().Matches(fullText))
        {
            var dupChar = m.Value[0];
            if (ValidDuplicates.Contains(dupChar)) continue;

            issues.Add(new QcIssueDto
            {
                IssueType = "text_error",
                SubType = "duplicate_char",
                Severity = "warning",
                OriginalText = m.Value,
                SuggestedText = dupChar.ToString(),
                Description = $"疑似重复字「{m.Value}」",
                Suggestion = $"请检查是否误写了两次「{dupChar}」",
            });
        }

        return issues;
    }
}
