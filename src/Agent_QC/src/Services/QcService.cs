using System.Diagnostics;
using Agent_QC.Models;
using Agent_QC.Services.Rules;

namespace Agent_QC.Services;

/// <summary>
/// QC 核心管线服务——4层流水线：预处理 → 格式 → 语义 → 逻辑规则 → 危急值。
/// </summary>
public class QcService : IQcService
{
    private readonly SectionParser _sectionParser = new();
    private readonly GenderConflictRule _genderConflictRule = new();
    private readonly AgeConflictRule _ageConflictRule = new();
    private readonly DirectionConflictRule _directionConflictRule = new();
    private readonly DeviceConflictRule _deviceConflictRule = new();
    private readonly ScanEnhanceConflictRule _scanEnhanceConflictRule = new();
    private readonly CriticalSignRule _criticalSignRule = new();

    public async Task<AjaxResult> ExecuteQcAsync(QcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReportId))
            return AjaxResult.Error(400, "ReportId 不能为空");

        var sw = Stopwatch.StartNew();

        // ── Level 0: 预处理 ──
        // SectionParser 解析段落边界（为 Phase 2 语义分析做准备）
        var fullText = (request.Findings ?? "") + " " + (request.Impression ?? "");

        // ── Level 3: 逻辑规则检测 ──
        var issues = new List<QcIssueDto>();
        issues.AddRange(_genderConflictRule.Check(request));
        issues.AddRange(_ageConflictRule.Check(request));
        issues.AddRange(_directionConflictRule.Check(request));
        issues.AddRange(_deviceConflictRule.Check(request));
        issues.AddRange(_scanEnhanceConflictRule.Check(request));

        // ── Level 4: 危急值检测 ──
        issues.AddRange(_criticalSignRule.Check(request));

        // ── 计算总分 ──
        sw.Stop();
        var totalScore = CalculateScore(issues);
        var passScore = 90m;

        var response = new QcResponse
        {
            ReportId = request.ReportId,
            TotalScore = totalScore,
            PassScore = passScore,
            Passed = totalScore >= passScore,
            QcLevel = "Level3+4",
            Issues = issues,
            Summary = issues.Count == 0
                ? "未发现问题"
                : $"发现 {issues.Count} 个问题",
            ProcessTimeMs = (int)sw.ElapsedMilliseconds,
        };

        return AjaxResult.Success(response);
    }

    /// <summary>
    /// 扣分制：起始100分，error -10，warning -5，critical -20，最低0。
    /// </summary>
    private static decimal CalculateScore(List<QcIssueDto> issues)
    {
        decimal score = 100;
        foreach (var issue in issues)
        {
            score -= issue.Severity switch
            {
                "critical" => 20,
                "error" => 10,
                _ => 5, // warning / info
            };
        }
        return Math.Max(0, score);
    }
}
