using Agent_QC.Models;

namespace Agent_QC.Services;

/// <summary>
/// 4维度评分引擎。
/// IssueType → Dimension 映射，维度内扣分，加权总分。
/// </summary>
public class ScoringEngine
{
    private const decimal PassThreshold = 90m;
    private const decimal StartScore = 100m;

    /// <summary>4维度定义：code, name, weight</summary>
    private static readonly (string Code, string Name, decimal Weight)[] Dimensions =
    {
        ("normative", "规范性", 0.30m),
        ("completeness", "完整性", 0.25m),
        ("logic", "逻辑性", 0.30m),
        ("timeliness", "时效性", 0.15m),
    };

    /// <summary>IssueType → Dimension Code 映射</summary>
    private static readonly Dictionary<string, string> IssueDimensionMap = new()
    {
        // 文本规范性 → normative
        ["text_error"] = "normative",
        ["format_error"] = "normative",
        ["terminology_error"] = "normative",
        ["terminology_nonstandard"] = "normative",
        // 完整性 → completeness
        ["completeness_error"] = "completeness",
        ["rads_missing"] = "completeness",
        // 逻辑矛盾 → logic
        ["gender_conflict"] = "logic",
        ["age_conflict"] = "logic",
        ["direction_conflict"] = "logic",
        ["device_conflict"] = "logic",
        ["scan_enhance_conflict"] = "logic",
        ["semantic_conflict"] = "logic",
        // 危急值 → timeliness
        ["critical_sign"] = "timeliness",
    };

    public QcResponse Calculate(QcResponse response)
    {
        // 每维度起始分
        var dimensionScores = Dimensions.ToDictionary(d => d.Code, _ => StartScore);

        foreach (var issue in response.Issues)
        {
            var dimCode = ResolveDimension(issue);
            var penalty = issue.Severity switch
            {
                "critical" => 20m,
                "error" => 10m,
                _ => 5m, // warning / info
            };
            dimensionScores[dimCode] = Math.Max(0, dimensionScores[dimCode] - penalty);
        }

        // 加权总分
        decimal total = 0;
        response.CheckItems = new List<QcCheckItem>();
        foreach (var (code, name, weight) in Dimensions)
        {
            var score = dimensionScores[code];
            total += score * weight;
            response.CheckItems.Add(new QcCheckItem
            {
                DimensionCode = code,
                DimensionName = name,
                Score = score,
                Weight = weight,
                Passed = score >= PassThreshold,
            });
        }

        response.TotalScore = Math.Round(total, 1);
        response.PassScore = PassThreshold;
        response.Passed = response.TotalScore >= PassThreshold;

        return response;
    }

    /// <summary>IssType → Dimension; fallback → normative</summary>
    private static string ResolveDimension(QcIssueDto issue)
    {
        if (!string.IsNullOrEmpty(issue.IssueType)
            && IssueDimensionMap.TryGetValue(issue.IssueType, out var dim))
            return dim;
        return "normative";
    }
}
