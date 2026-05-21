using System.Text.RegularExpressions;
using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level1;

/// <summary>
/// 单位/格式规范——乘法符号检测（x→×）+ 相邻尺寸单位一致性。
/// </summary>
public partial class UnitFormatRule
{
    [GeneratedRegex(@"\d\s*[xX]\s*\d", RegexOptions.Compiled)]
    private static partial Regex BadMultiplyPattern();

    [GeneratedRegex(@"\d+\.?\d*\s*(cm|mm)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SizePattern();

    public List<QcIssueDto> Check(QcRequest request)
    {
        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();

        // 乘法符号规范：x/X 应改为 ×
        foreach (Match m in BadMultiplyPattern().Matches(fullText))
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "format_error",
                SubType = "multiply_sign",
                Severity = "warning",
                OriginalText = m.Value,
                SuggestedText = m.Value.Replace('x', '×').Replace('X', '×'),
                Description = $"乘法符号不规范「{m.Value}」，应使用 ×",
                Suggestion = "请使用 × 代替 x/X 作为乘法符号",
            });
        }

        // 相邻尺寸单位不一致检测
        var szMatches = SizePattern().Matches(fullText);
        for (int i = 0; i < szMatches.Count - 1; i++)
        {
            var m1 = szMatches[i];
            var m2 = szMatches[i + 1];
            if (m2.Index - (m1.Index + m1.Length) >= 20) continue;

            var unit1 = m1.Groups[1].Value.ToLower();
            var unit2 = m2.Groups[1].Value.ToLower();
            if (unit1 != unit2)
            {
                issues.Add(new QcIssueDto
                {
                    IssueType = "format_error",
                    SubType = "unit_mismatch",
                    Severity = "warning",
                    OriginalText = $"{m1.Value} ... {m2.Value}",
                    Description = $"相邻尺寸描述单位不一致（{unit1} vs {unit2}）",
                    Suggestion = "建议统一使用同一单位（cm或mm）",
                });
                break;
            }
        }

        return issues;
    }
}
