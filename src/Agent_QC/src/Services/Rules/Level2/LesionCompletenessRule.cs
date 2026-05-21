using System.Text.RegularExpressions;
using Agent_QC.Models;

namespace Agent_QC.Services.Rules.Level2;

/// <summary>
/// 病灶描述完整性——有病灶时必须包含尺寸和形态特征描述。
/// </summary>
public partial class LesionCompletenessRule
{
    private static readonly string[] LesionWords =
    {
        "结节", "肿块", "占位", "病灶", "阴影", "囊肿", "瘤",
        "占位性病变", "异常密度", "异常信号", "异常回声",
    };

    private static readonly string[] SizeWords =
        { "大小", "直径", "约", "cm", "mm", "长径", "短径" };

    [GeneratedRegex(@"(\d+\s*(个|枚|处)|共\s*\d+\s*(个|枚|处)|约\s*\d+\s*(个|枚|处))", RegexOptions.Compiled)]
    private static partial Regex CountPattern();

    public List<QcIssueDto> Check(QcRequest request)
    {
        var findings = request.Findings ?? "";
        var issues = new List<QcIssueDto>();

        var hasLesion = LesionWords.Any(f => findings.Contains(f, StringComparison.Ordinal));
        if (!hasLesion) return issues;

        // 尺寸检查
        var hasSize = SizeWords.Any(f => findings.Contains(f, StringComparison.Ordinal));
        if (!hasSize)
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "terminology_error",
                SubType = "missing_size",
                Severity = "error",
                Description = "病灶描述缺少精确尺寸（如「大小约1.5cm」）",
                Suggestion = "所有可测量的病变必须标注具体大小和单位",
            });
        }

        // 多发性但未标注具体数量
        var hasMultiple = findings.Contains("多发性") || findings.Contains("多发")
            || findings.Contains("数个") || findings.Contains("若干");
        if (hasMultiple && !CountPattern().IsMatch(findings))
        {
            issues.Add(new QcIssueDto
            {
                IssueType = "terminology_error",
                SubType = "missing_count",
                Severity = "warning",
                Description = "使用「多发/数个」但未标注具体数量",
                Suggestion = "请使用「N个/N枚/N处」等精确数量描述",
            });
        }

        return issues;
    }
}
