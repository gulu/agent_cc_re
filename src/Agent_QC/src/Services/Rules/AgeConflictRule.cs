using Agent_QC.Models;

namespace Agent_QC.Services.Rules;

/// <summary>
/// 年龄-诊断矛盾检测。某些诊断在特定年龄组极少出现，
/// 如0-20岁出现骨质疏松、0-18岁出现骨质增生等。
/// </summary>
public class AgeConflictRule
{
    /// <summary>诊断关键词 → (最小年龄, 最大年龄, 严重级别)</summary>
    private static readonly (string Keyword, int MinAge, int MaxAge, string Severity)[] AgeRules =
    {
        ("骨质疏松", 0, 20, "error"),
        ("骨质增生", 0, 18, "warning"),
        ("脑梗灶", 0, 12, "error"),
        ("脑白质疏松", 0, 20, "warning"),
        ("前列腺增生", 0, 30, "error"),
        ("退行性变", 0, 20, "warning"),
        ("恶性肿瘤", 0, 2, "error"),
        ("肝硬化", 0, 10, "error"),
        ("冠状动脉硬化", 0, 15, "error"),
        ("颈椎病", 0, 18, "warning"),
        ("腰椎间盘突出", 0, 15, "warning"),
        ("动脉粥样硬化", 0, 15, "error"),
        ("脑萎缩", 0, 30, "warning"),
        ("骨关节病", 0, 18, "warning"),
    };

    public List<QcIssueDto> Check(QcRequest request)
    {
        if (request.PatientAge == null)
            return new List<QcIssueDto>();

        var age = request.PatientAge.Value;
        var fullText = (request.Findings ?? "") + (request.Impression ?? "");
        var issues = new List<QcIssueDto>();

        foreach (var (keyword, minAge, maxAge, severity) in AgeRules)
        {
            if (!fullText.Contains(keyword, StringComparison.Ordinal)) continue;
            if (age < minAge || age > maxAge) continue;

            issues.Add(new QcIssueDto
            {
                IssueType = "age_conflict",
                Severity = severity,
                Description = $"患者年龄{age}岁，出现「{keyword}」需确认（一般{minAge}-{maxAge}岁不常见）",
            });
        }

        return issues;
    }
}
