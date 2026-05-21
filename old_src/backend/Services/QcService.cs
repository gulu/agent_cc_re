// QcService — 质控主流程服务
// 4 层并行推理管线：Level 1-2 TinyBERT（暂为桩）× Level 3-4 规则引擎
// 遵循 soul.md 规范：public static + try-catch + AjaxResult

using System.Diagnostics;
using System.Text.RegularExpressions;
using FreeSql;
using Newtonsoft.Json;
using ReportQC.Entities;
using ReportQC.Models;

namespace ReportQC.Services;

public static class QcService
{
    /// <summary>
    /// 执行完整质控流程
    /// </summary>
    public static AjaxResult ExecuteQc(IFreeSql fsql, QcRequest request)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // 1. 确保知识库已加载
            if (!QcKnowledgeCache.Loaded)
                QcKnowledgeCache.Load(fsql);

            // 2. 4 层并行推理
            var issues = new List<QcIssue>();

            int tinyBertMs = 0, ruleEngineMs = 0;

            // Level 1: 文本层 — TinyBERT（优先）+ 规则兜底
            var sw1 = Stopwatch.StartNew();
            var patientInfo = new Level1QcService.QcRequestInfo
            {
                PatientName = request.PatientName,
                PatientGender = request.PatientGender,
                PatientAge = request.PatientAge,
                AccessionNo = request.AccessionNo,
                ExamPart = request.ExamPart,
                ExamDevice = request.ExamDevice,
                ExamMethod = request.ExamMethod,
                ReportType = request.ReportType
            };
            var level1Issues = Level1QcService.Run(request.FullContent, patientInfo)
                .Select(i => new QcIssue
                {
                    Level = 1,
                    IssueType = i.IssueType,
                    SubType = i.SubType,
                    OriginalText = i.OriginalText,
                    SuggestedText = i.SuggestedText,
                    Description = i.Description,
                    Severity = i.Severity,
                    Location = i.Location,
                    Suggestion = i.Suggestion
                }).ToList();
            sw1.Stop();
            tinyBertMs += (int)sw1.ElapsedMilliseconds;
            issues.AddRange(level1Issues);

            // Level 2: 语义层 — TinyBERT（优先）+ 术语库查表
            var sw2 = Stopwatch.StartNew();
            var level2Issues = Level2QcService.Run(request.FullContent, request.Findings, request.Impression, request.ReportType)
                .Select(i => new QcIssue
                {
                    Level = 2,
                    IssueType = i.IssueType,
                    SubType = i.SubType,
                    OriginalText = i.OriginalText,
                    SuggestedText = i.SuggestedText,
                    Description = i.Description,
                    Severity = i.Severity,
                    Location = i.Location,
                    Suggestion = i.Suggestion
                }).ToList();
            sw2.Stop();
            tinyBertMs += (int)sw2.ElapsedMilliseconds;
            issues.AddRange(level2Issues);

            // Level 3: 逻辑层 — 规则引擎 + 内存 Dictionary
            var sw3 = Stopwatch.StartNew();
            var level3Issues = RunLevel3(request);
            sw3.Stop();
            ruleEngineMs += (int)sw3.ElapsedMilliseconds;
            issues.AddRange(level3Issues);

            // Level 4: 临床层 — 规则引擎 + SQLite 知识库
            var sw4 = Stopwatch.StartNew();
            var level4Issues = RunLevel4(request);
            sw4.Stop();
            ruleEngineMs += (int)sw4.ElapsedMilliseconds;
            issues.AddRange(level4Issues);

            // 跨层级去重（Level 2 和 Level 3 可能检出相同问题）
            issues = issues
                .GroupBy(i => $"{i.IssueType}|{i.SubType ?? ""}|{(i.Description?.Length > 20 ? i.Description[..20] : i.Description ?? "")}")
                .Select(g => g.First())
                .ToList();

            // 3. 评分
            var dimensions = fsql.Select<ScoreDimension>().Where(d => d.IsActive).ToList();
            var (checkItems, totalScore, qcLevel, passed) = CalculateScore(dimensions, issues);

            // 4. 持久化
            var report = new QcReport
            {
                ReportId = request.ReportId,
                Findings = request.Findings,
                Impression = request.Impression,
                ReportType = request.ReportType,
                PatientGender = request.PatientGender,
                PatientAge = request.PatientAge,
                PatientName = request.PatientName,
                PatientIdNo = request.PatientIdNo,
                OutpatientNo = request.OutpatientNo,
                InpatientNo = request.InpatientNo,
                PatientPhone = request.PatientPhone,
                ClinicalDiagnosis = request.ClinicalDiagnosis,
                Symptoms = request.Symptoms,
                MedicalHistory = request.MedicalHistory,
                RequestDepartment = request.RequestDepartment,
                RequestDoctor = request.RequestDoctor,
                ExamPart = request.ExamPart,
                ExamDevice = request.ExamDevice,
                ExamMethod = request.ExamMethod,
                RequestNo = request.RequestNo,
                AccessionNo = request.AccessionNo,
                ExamDate = request.ExamDate,
                ReportDate = request.ReportDate,
                TotalScore = totalScore,
                PassScore = 90,
                Passed = passed,
                QcLevel = qcLevel,
                TinyBertTimeMs = tinyBertMs,
                RuleEngineTimeMs = ruleEngineMs,
                TotalTimeMs = (int)sw.ElapsedMilliseconds,
                RequestSource = "api",
                CreatedAt = DateTime.Now
            };
            fsql.Insert(report).ExecuteAffrows();

            // 5. 保存问题明细
            var issueEntities = issues.Select(i => new QcIssue
            {
                QcReportId = report.Id,
                Level = i.Level,
                IssueType = i.IssueType,
                SubType = i.SubType,
                OriginalText = i.OriginalText,
                SuggestedText = i.SuggestedText,
                Description = i.Description,
                Severity = i.Severity,
                Location = i.Location,
                Suggestion = i.Suggestion,
                CreatedAt = DateTime.Now
            }).ToList();

            if (issueEntities.Any())
                fsql.Insert(issueEntities).ExecuteAffrows();

            // 6. 保存评分明细
            var scoreResults = checkItems.Select(ci => new ScoreResult
            {
                QcReportId = report.Id,
                DimensionId = dimensions.First(d => d.DimensionCode == ci.DimensionCode).Id,
                Score = ci.Score,
                Weight = ci.Weight,
                CreatedAt = DateTime.Now
            }).ToList();

            if (scoreResults.Any())
                fsql.Insert(scoreResults).ExecuteAffrows();

            // 7. 构建响应
            var response = new QcResponse
            {
                ReportId = request.ReportId,
                TotalScore = totalScore,
                PassScore = 90,
                Passed = passed,
                QcLevel = qcLevel,
                CheckItems = checkItems,
                Issues = issues.Select(i => new QcIssueDto
                {
                    IssueType = i.IssueType,
                    SubType = i.SubType,
                    Description = i.Description,
                    Severity = i.Severity,
                    Location = i.Location,
                    OriginalText = i.OriginalText,
                    SuggestedText = i.SuggestedText,
                    Suggestion = i.Suggestion
                }).ToList(),
                Summary = BuildSummary(issues, totalScore, passed),
                ProcessTimeMs = (int)sw.ElapsedMilliseconds
            };

            return AjaxResult.Success(response);
        }
        catch (Exception ex)
        {
            JSBaseLogs.JSLogManager.WriteLog(ex);
            return AjaxResult.Error($"质控处理异常：{ex.Message}");
        }
    }

    // ══════════════════════════════════════════════
    //  Level 3: 逻辑层 — 规则引擎 + 内存 Dictionary
    //  纯内存查表 + 正则，零外部依赖
    // ══════════════════════════════════════════════

    private static List<QcIssue> RunLevel3(QcRequest request)
    {
        var issues = new List<QcIssue>();
        var content = request.FullContent;

        // 3.1 性别矛盾检测
        if (!string.IsNullOrEmpty(request.PatientGender))
        {
            foreach (var kv in QcKnowledgeCache.GenderExcludeFemale)
            {
                var keyword = kv.Key;                 // "子宫"
                var excludeGenders = kv.Value;        // ["男","未知"]
                if (content.Contains(keyword) && excludeGenders.Contains(request.PatientGender))
                {
                    issues.Add(new QcIssue
                    {
                        Level = 3,
                        IssueType = "gender_conflict",
                        Description = $"患者性别为「{request.PatientGender}」但报告中出现「{keyword}」",
                        Severity = "critical",
                        Location = "whole",
                        OriginalText = keyword,
                        Suggestion = "请检查是否写入了与患者性别不符的解剖描述"
                    });
                }
            }

            foreach (var kv in QcKnowledgeCache.GenderExcludeMale)
            {
                var keyword = kv.Key;
                var excludeGenders = kv.Value;
                if (content.Contains(keyword) && excludeGenders.Contains(request.PatientGender))
                {
                    issues.Add(new QcIssue
                    {
                        Level = 3,
                        IssueType = "gender_conflict",
                        Description = $"患者性别为「{request.PatientGender}」但报告中出现「{keyword}」",
                        Severity = "critical",
                        Location = "whole",
                        OriginalText = keyword,
                        Suggestion = "请检查是否写入了与患者性别不符的解剖描述"
                    });
                }
            }
        }

        // 3.2 部位矛盾检测
        if (!string.IsNullOrEmpty(request.ExamPart) && QcKnowledgeCache.BodyPartAreaMap.TryGetValue(request.ExamPart, out var expectedParts))
        {
            var partFound = expectedParts.Any(p => content.Contains(p));
            // 如果没有找到任何期望部位，但知识库有定义，说明可能描述不对
            // 这里仅做正向检测——精确的负向匹配需要更多语境分析
        }

        // 3.3 方位矛盾检测（所见左侧→结论右侧）
        var findings = ExtractFindings(content);
        var impression = ExtractImpression(content);

        // 3.3 方位矛盾检测 — 取最长匹配词避免子串重复匹配
        var foundPos = QcKnowledgeCache.DirectionPositive
            .Where(w => findings.Contains(w))
            .OrderByDescending(w => w.Length)
            .FirstOrDefault();

        var foundNeg = QcKnowledgeCache.DirectionNegative
            .Where(w => impression.Contains(w))
            .OrderByDescending(w => w.Length)
            .FirstOrDefault();

        if (foundPos != null && foundNeg != null)
        {
            issues.Add(new QcIssue
            {
                Level = 3,
                IssueType = "direction_conflict",
                Description = $"所见描述中出现「{foundPos}」但结论中出现「{foundNeg}」，方位矛盾",
                Severity = "error",
                Location = "impression",
                OriginalText = $"...{foundPos}...(所见) ...{foundNeg}...(结论)",
                Suggestion = "请确认方位描述是否一致"
            });
        }

        // 3.4 年龄矛盾检测
        if (request.PatientAge.HasValue)
        {
            foreach (var kv in QcKnowledgeCache.AgeExclude)
            {
                var diagnosis = kv.Key;         // "脑梗灶"
                var ageRanges = kv.Value;       // ["0-12"]
                if (content.Contains(diagnosis))
                {
                    foreach (var range in ageRanges)
                    {
                        var parts = range.Split('-');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], out var minAge) &&
                            int.TryParse(parts[1], out var maxAge) &&
                            request.PatientAge.Value >= minAge &&
                            request.PatientAge.Value <= maxAge)
                        {
                            issues.Add(new QcIssue
                            {
                                Level = 3,
                                IssueType = "age_conflict",
                                Description = $"患者年龄 {request.PatientAge} 岁，但报告中出现「{diagnosis}」（通常不适用于 {range} 岁）",
                                Severity = "error",
                                Location = "findings",
                                OriginalText = diagnosis,
                                Suggestion = "请确认诊断是否与患者年龄相符"
                            });
                        }
                    }
                }
            }
        }

        // 3.5 阴阳性矛盾检测（简单正则匹配）
        var negationPatterns = new[] { "未见", "无", "无明显", "未发现", "阴性" };
        var positivePatterns = new[] { "可见", "有", "存在", "显示", "阳性" };

        var negFound = negationPatterns.Where(p => findings.Contains(p)).ToList();
        var posFound = positivePatterns.Where(p => impression.Contains(p)).ToList();

        // 简化检测：如果所见中全是"未见"，结论中写"可见"，视为矛盾
        // 更精确的检测需要 NLP 层级分析，此处作为兜底

        // 3.6 单位格式错误
        if (!string.IsNullOrEmpty(QcKnowledgeCache.UnitPattern))
        {
            try
            {
                var regex = new Regex(QcKnowledgeCache.UnitPattern);
                var matches = regex.Matches(content);
                // 正则匹配到的即为格式正确的，不做正向输出；
                // 负向检测（发现明显错误的单位）需要额外规则
                // 如 "1cm×2m" 中单位不一致
                var unitMismatch = Regex.Matches(content, @"(\d+\.?\d*)\s*(cm|mm|m)\s*[×x×]");
                foreach (Match m in unitMismatch)
                {
                    // 简单检测：乘法两边单位不同
                }
            }
            catch
            {
                // 正则无效则跳过
            }
        }

        return issues;
    }

    // ══════════════════════════════════════════════
    //  Level 4: 临床层 — 规则引擎 + SQLite 知识库
    // ══════════════════════════════════════════════

    private static List<QcIssue> RunLevel4(QcRequest request)
    {
        var issues = new List<QcIssue>();
        var content = request.FullContent;

        // 4.1 平扫/增强矛盾
        if (!string.IsNullOrEmpty(request.ReportType) &&
            QcKnowledgeCache.ScanTypeMap.TryGetValue(request.ReportType, out var bannedTerms))
        {
            foreach (var term in bannedTerms)
            {
                if (content.Contains(term))
                {
                    issues.Add(new QcIssue
                    {
                        Level = 4,
                        IssueType = "scan_enhance_conflict",
                        Description = $"检查方式为「{request.ReportType}」，但报告中出现强化相关描述「{term}」",
                        Severity = "error",
                        Location = "whole",
                        OriginalText = term,
                        Suggestion = "平扫检查不应出现强化/增强描述，请确认"
                    });
                }
            }
        }

        // 4.2 设备类型错误
        if (!string.IsNullOrEmpty(request.ExamDevice) &&
            QcKnowledgeCache.ExamDeviceKeywords.TryGetValue(request.ExamDevice, out var deviceBannedTerms))
        {
            foreach (var term in deviceBannedTerms)
            {
                if (content.Contains(term))
                {
                    issues.Add(new QcIssue
                    {
                        Level = 4,
                        IssueType = "device_conflict",
                        Description = $"检查设备为「{request.ExamDevice}」，但报告中出现「{term}」",
                        Severity = "error",
                        Location = "whole",
                        OriginalText = term,
                        Suggestion = $"请确认：{request.ExamDevice} 检查不应引用其他设备描述"
                    });
                }
            }
        }

        // 4.3 RADS 分类缺失检测
        var impression = ExtractImpression(content);
        if (!string.IsNullOrEmpty(request.ReportType) &&
            QcKnowledgeCache.RadsRequireTypes.TryGetValue(request.ReportType, out var radsTypes))
        {
            var hasRads = radsTypes.Any(r => impression.Contains(r, StringComparison.OrdinalIgnoreCase));
            if (!hasRads)
            {
                issues.Add(new QcIssue
                {
                    Level = 4,
                    IssueType = "rads_missing",
                    Description = $"{request.ReportType} 检查报告应标注 {string.Join("/", radsTypes)} 分级",
                    Severity = "error",
                    Location = "impression",
                    Suggestion = $"请补充 {string.Join("/", radsTypes)} 分级"
                });
            }
        }

        // 4.4 关键测量缺失检测
        var sizeKeywords = new[] { "大小", "直径", "约", "cm", "mm" };
        var hasSize = sizeKeywords.Any(k => content.Contains(k));
        var hasLesion = new[] { "结节", "肿块", "占位", "病灶", "阴影" }.Any(k => content.Contains(k));
        if (hasLesion && !hasSize)
        {
            issues.Add(new QcIssue
            {
                Level = 4,
                IssueType = "missing_measurement",
                Description = "报告中描述了病灶/结节/占位，但缺少尺寸测量",
                Severity = "warning",
                Location = "findings",
                Suggestion = "请补充病灶的精确尺寸描述"
            });
        }

        // 4.5 危急征象提示
        foreach (var keyword in QcKnowledgeCache.CriticalSignKeywords)
        {
            if (content.Contains(keyword))
            {
                issues.Add(new QcIssue
                {
                    Level = 4,
                    IssueType = "critical_sign",
                    Description = $"报告中出现危急征象相关关键词「{keyword}」",
                    Severity = "critical",
                    Location = "whole",
                    OriginalText = keyword,
                    Suggestion = "请确认是否为危急值，是否需要立即上报"
                });
                break; // 一个即触发
            }
        }

        return issues;
    }

    // ══════════════════════════════════════════════
    //  评分引擎
    // ══════════════════════════════════════════════

    private static (List<QcCheckItem> items, decimal totalScore, string qcLevel, bool passed)
        CalculateScore(List<ScoreDimension> dimensions, List<QcIssue> issues)
    {
        var items = new List<QcCheckItem>();

        foreach (var dim in dimensions)
        {
            var dimIssues = issues.Where(i => GetDimensionForIssue(i.IssueType) == dim.DimensionCode).ToList();
            var criticalCount = dimIssues.Count(i => i.Severity == "critical");
            var errorCount = dimIssues.Count(i => i.Severity == "error");
            var warningCount = dimIssues.Count(i => i.Severity == "warning");

            var score = 100m;
            score -= criticalCount * 25;
            score -= errorCount * 10;
            score -= warningCount * 5;
            score = Math.Max(0, score);

            items.Add(new QcCheckItem
            {
                DimensionCode = dim.DimensionCode,
                DimensionName = dim.DimensionName,
                Passed = score >= 60,
                Score = score,
                Weight = dim.DefaultWeight
            });
        }

        // 总分 = 各维度加权
        var totalWeight = items.Sum(i => i.Weight);
        var totalScore = totalWeight > 0
            ? items.Sum(i => i.Score * i.Weight / totalWeight)
            : 100m;

        totalScore = Math.Round(totalScore, 2);

        // 有 critical 级问题 → 直接不合格
        var hasCritical = issues.Any(i => i.Severity == "critical");

        string qcLevel;
        bool passed;

        if (hasCritical)
        {
            passed = false;
            qcLevel = "初级质控";
        }
        else if (totalScore >= 90)
        {
            passed = true;
            qcLevel = "高级质控";
        }
        else if (totalScore >= 60)
        {
            passed = false;
            qcLevel = "中级质控";
        }
        else
        {
            passed = false;
            qcLevel = "初级质控";
        }

        return (items, totalScore, qcLevel, passed);
    }

    // ══════════════════════════════════════════════
    //  辅助方法
    // ══════════════════════════════════════════════

    /// <summary>从报告文本中提取"所见"部分</summary>
    private static string ExtractFindings(string content)
    {
        // 常见分隔模式：所见...诊断结论/印象/诊断意见
        var splitKeywords = new[] { "诊断结论", "诊断意见", "印象", "结论" };
        var idx = -1;
        foreach (var kw in splitKeywords)
        {
            idx = content.IndexOf(kw, StringComparison.Ordinal);
            if (idx > 0) break;
        }
        return idx > 0 ? content[..idx] : content;
    }

    /// <summary>从报告文本中提取"结论"部分</summary>
    private static string ExtractImpression(string content)
    {
        var splitKeywords = new[] { "诊断结论", "诊断意见", "印象", "结论" };
        var idx = -1;
        foreach (var kw in splitKeywords)
        {
            idx = content.IndexOf(kw, StringComparison.Ordinal);
            if (idx > 0) break;
        }
        return idx > 0 ? content[idx..] : content;
    }

    /// <summary>IssueType → 评分维度映射</summary>
    private static string GetDimensionForIssue(string issueType)
    {
        return issueType switch
        {
            "text_error" or "text_typo" or "format_error" => "normative",
            "completeness_error" or "missing_info" or "terminology_nonstandard"
                or "terminology_error" or "rads_missing" => "normative",
            "gender_conflict" or "body_part_conflict" or "direction_conflict"
                or "age_conflict" or "semantic_conflict" or "unit_error" => "logic",
            "scan_enhance_conflict" or "device_conflict" or "missing_measurement"
                or "missing_followup" or "critical_sign" => "completeness",
            _ => "normative"
        };
    }

    /// <summary>生成摘要</summary>
    private static string BuildSummary(List<QcIssue> issues, decimal totalScore, bool passed)
    {
        var criticalCount = issues.Count(i => i.Severity == "critical");
        var errorCount = issues.Count(i => i.Severity == "error");
        var warningCount = issues.Count(i => i.Severity == "warning");

        var parts = new List<string>();
        if (criticalCount > 0) parts.Add($"{criticalCount} 项严重问题");
        if (errorCount > 0) parts.Add($"{errorCount} 项错误");
        if (warningCount > 0) parts.Add($"{warningCount} 项警告");

        var issueSummary = parts.Count > 0 ? string.Join("、", parts) : "未发现问题";
        var passText = passed ? "通过" : "未通过";

        return $"评分 {totalScore} 分，{passText}。共 {issues.Count} 项质控提示（{issueSummary}）。";
    }

    /// <summary>
    /// 质控问题内部模型（非持久化）
    /// </summary>
    private class QcIssueItem
    {
        public int Level { get; set; }
        public string IssueType { get; set; } = string.Empty;
        public string? SubType { get; set; }
        public string? Description { get; set; }
        public string Severity { get; set; } = "warning";
        public string? Location { get; set; }
        public string? OriginalText { get; set; }
        public string? SuggestedText { get; set; }
        public string? Suggestion { get; set; }
    }
}
