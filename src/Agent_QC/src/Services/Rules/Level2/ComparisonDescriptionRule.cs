using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level2;

/// <summary>
/// 对比描述规范性——有"与前片比较"等对比词时，必须包含比较结论。
/// </summary>
public class ComparisonDescriptionRule
{
    private static readonly string[] CompareWords =
        { "与前片比较", "与既往比较", "与前次比较", "对比前片", "对照前次", "与前片相比" };

    private static readonly string[] CompareResults =
        { "无明显变化", "增大", "缩小", "增多", "减少", "好转", "加重", "稳定",
          "大致同前", "基本同前", "相仿", "相似", "进展", "消退", "吸收" };

    public List<QcIssueDto> Check(QcRequest request)
    {
        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();

        foreach (var cw in CompareWords)
        {
            if (!fullText.Contains(cw, StringComparison.Ordinal)) continue;

            var idx = fullText.IndexOf(cw, StringComparison.Ordinal);
            var after = idx + cw.Length < fullText.Length
                ? fullText.Substring(idx + cw.Length, Math.Min(100, fullText.Length - idx - cw.Length))
                : "";

            if (!CompareResults.Any(r => after.Contains(r, StringComparison.Ordinal)))
            {
                issues.Add(new QcIssueDto
                {
                    IssueType = "terminology_error",
                    SubType = "missing_comparison_result",
                    Severity = "warning",
                    OriginalText = cw,
                    Description = $"描述了与既往检查比较（「{cw}」），但缺少比较结论",
                    Suggestion = "请在比较描述后补充明确的比较结论（如「无明显变化」「增大」「缩小」等）",
                });
            }
            break;
        }

        return issues;
    }
}
