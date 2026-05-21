using System.Diagnostics;
using Agent_QC.Models;
using Agent_QC.Services.Rules;
using Agent_QC.Services.Rules.Level1;
using Agent_QC.Services.Rules.Level2;

namespace Agent_QC.Services;

/// <summary>
/// QC 核心管线服务——5层流水线：
/// Level 0 预处理 → Level 1 文本格式 → Level 2 语义规范 → Level 3 逻辑规则 → Level 4 危急值。
/// </summary>
public class QcService : IQcService
{
    // Level 0
    private readonly SectionParser _sectionParser = new();

    // Level 1: 文本格式
    private readonly PhraseTypoRule _phraseTypoRule = new();
    private readonly DuplicateCharRule _duplicateCharRule = new();
    private readonly UnitFormatRule _unitFormatRule = new();
    private readonly SentencePunctuationRule _sentencePunctuationRule = new();
    private readonly PatientInfoRule _patientInfoRule = new();
    private readonly TerminologyStandardRule _terminologyStandardRule = new();

    // Level 2: 语义规范
    private readonly ColloquialTermRule _colloquialTermRule = new();
    private readonly AnatomyTermRule _anatomyTermRule = new();
    private readonly LesionCompletenessRule _lesionCompletenessRule = new();
    private readonly RadsClassificationRule _radsClassificationRule = new();
    private readonly FindingsImpressionConsistencyRule _findingsImpressionConsistencyRule = new();
    private readonly ComparisonDescriptionRule _comparisonDescriptionRule = new();
    private readonly AdviceConsistencyRule _adviceConsistencyRule = new();

    // Level 3: 逻辑规则
    private readonly GenderConflictRule _genderConflictRule = new();
    private readonly AgeConflictRule _ageConflictRule = new();
    private readonly DirectionConflictRule _directionConflictRule = new();
    private readonly DeviceConflictRule _deviceConflictRule = new();
    private readonly ScanEnhanceConflictRule _scanEnhanceConflictRule = new();

    // Level 4: 危急值
    private readonly CriticalSignRule _criticalSignRule = new();

    // 评分
    private readonly ScoringEngine _scoringEngine = new();

    public async Task<AjaxResult> ExecuteQcAsync(QcRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ReportId))
            return AjaxResult.Error(400, "ReportId 不能为空");

        var sw = Stopwatch.StartNew();
        var issues = new List<QcIssueDto>();

        // ── Level 1: 文本格式 ──
        issues.AddRange(_phraseTypoRule.Check(request));
        issues.AddRange(_duplicateCharRule.Check(request));
        issues.AddRange(_unitFormatRule.Check(request));
        issues.AddRange(_sentencePunctuationRule.Check(request));
        issues.AddRange(_patientInfoRule.Check(request));
        issues.AddRange(_terminologyStandardRule.Check(request));

        // ── Level 2: 语义规范 ──
        issues.AddRange(_colloquialTermRule.Check(request));
        issues.AddRange(_anatomyTermRule.Check(request));
        issues.AddRange(_lesionCompletenessRule.Check(request));
        issues.AddRange(_radsClassificationRule.Check(request));
        issues.AddRange(_findingsImpressionConsistencyRule.Check(request));
        issues.AddRange(_comparisonDescriptionRule.Check(request));
        issues.AddRange(_adviceConsistencyRule.Check(request));

        // ── Level 3: 逻辑规则 ──
        issues.AddRange(_genderConflictRule.Check(request));
        issues.AddRange(_ageConflictRule.Check(request));
        issues.AddRange(_directionConflictRule.Check(request));
        issues.AddRange(_deviceConflictRule.Check(request));
        issues.AddRange(_scanEnhanceConflictRule.Check(request));

        // ── Level 4: 危急值 ──
        issues.AddRange(_criticalSignRule.Check(request));

        sw.Stop();

        // 4维度评分
        var response = new QcResponse
        {
            ReportId = request.ReportId,
            QcLevel = "L1+L2+L3+L4",
            Issues = issues,
            ProcessTimeMs = (int)sw.ElapsedMilliseconds,
        };
        _scoringEngine.Calculate(response);

        response.Summary = issues.Count == 0
            ? "未发现问题"
            : $"发现 {issues.Count} 个问题";

        return AjaxResult.Success(response);
    }
}
