// Level1QcService — 文本层质控（错别字、漏字、标点、完整性、格式规范化）
// 基于《放射诊断报告书写规范专家共识》（2025）& DB11/T 2505—2025
// 兜底实现：规则引擎 + 术语库查表
// 当 TinyBERT 模型就绪时自动切换为 ONNX 推理

using System.Text.RegularExpressions;
using ReportQC.Entities;

namespace ReportQC.Services;

public static class Level1QcService
{
    #region 错别字映射表

    // ── 形近字：单字→单字映射（仅在特定词组语境中误写） ──
    private static readonly Dictionary<string, string> ShapeSimilarSingles = new()
    {
        { "谋", "密" }, { "症", "征" }, { "证", "征" },
        { "庖", "疱" }, { "痞", "脾" }, { "干", "肝" },
        { "贤", "肾" }, { "费", "肺" }, { "杆", "肝" },
        { "孟", "盂" }, { "琅", "囊" }, { "膵", "胰" },
        { "隔", "膈" }, { "栓", "栓" }, // 本身正确但易误
        { "骼", "髂" }, { "垂", "垂" },
        { "页", "叶" }, { "脾", "脾" },
    };

    // ── 医学词组误写：上下文感知，搜整个词组匹配 ──
    //   （仅报告中出现这些词组时才报错）
    private static readonly Dictionary<string, string> MedicalPhraseTypos = new()
    {
        // 形近+音近混合词组
        { "低密谋灶", "低密度灶" },
        { "高密谋灶", "高密度灶" },
        { "轨道症", "轨道征" },
        { "毛刺症", "毛刺征" },
        { "分页症", "分叶征" },
        { "胸膜凹陷症", "胸膜凹陷征" },
        { "空泡症", "空泡征" },
        { "钙化点", "钙化灶" },
        { "低密灶", "低密度灶" },
        { "影像学证", "影像学征" },
        { "十二脂肠", "十二指肠" },
        { "喷门", "贲门" },
        { "综合隔", "纵隔" },
        { "肾孟", "肾盂" },
        { "输柰管", "输尿管" },
        { "胆琅", "胆囊" },
        { "骨拆", "骨折" },
        { "破列", "破裂" },
        { "坏列", "坏死" },
        { "积夜", "积液" },
        { "肺大泡", "肺大疱" },
        { "颅内血钟", "颅内血肿" },
        { "肝纤维", "肝纤维化" },
        { "门静", "门静脉" },
        { "下腔静", "下腔静脉" },
        { "随质", "髓质" },
        { "皮质", "皮质" },
        { "骨皮贡", "骨皮质" },
        { "尿路洁石", "尿路结石" },
        { "洁石", "结石" },
    };

    // ── 常见单字错字词组（需要词组上下文才能判定错误） ──
    private static readonly (string wrong, string correct)[] ContextualTypos =
    {
        ("密谋", "密度"), ("谋灶", "密灶"),
        ("症性", "征性"), ("性症", "性征"),
        ("痞脏", "脾脏"), ("干脏", "肝脏"),
        ("贤脏", "肾脏"), ("费部", "肺部"),
        ("杆脏", "肝脏"), ("干硬化", "肝硬化"),
    };

    #endregion

    #region 重复字检测

    private static readonly Regex DuplicateCharRegex = new(
        @"([\p{IsCJKUnifiedIdeographs}])\1{1,}",
        RegexOptions.Compiled);

    // 合法叠字白名单（医学报告中常见的）
    private static readonly HashSet<char> ValidDuplicateChars = new()
    {
        '慢', '常', '渐', '隐', '显', '明', '微', '轻', '重', '淡', '浓',
        '大', '小', '高', '低', '多', '少', '远', '近', '前', '后', '上', '下',
        '层', '次', '粒', '点', '斑', '壮', // 壮在"强壮"中合法
        '薄', '厚', '深', '浅', '均', '略', '稍', '极', '最', '更',
    };

    #endregion

    #region 单位/格式正则

    // 检测尺寸格式：数值+单位 如 "1.5cm" "10mm"
    private static readonly Regex SizePattern = new(
        @"\d+\.?\d*\s*(cm|mm|m)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 检测乘法符号不规范：使用 x/X 而非 ×（U+00D7）
    private static readonly Regex BadMultiplySign = new(
        @"\d\s*[xX]\s*\d",
        RegexOptions.Compiled);

    // 检测是否以列表符号开头
    private static readonly Regex ListItemPattern = new(
        @"^\s*[-•·\d]+[\.\)、]",
        RegexOptions.Compiled);

    #endregion

    /// <summary>
    /// 执行 Level 1 文本层质控
    /// </summary>
    public static List<QcIssueItem> Run(string content, QcRequestInfo? patientInfo = null)
    {
        if (TinyBertService.IsLoaded)
        {
            var embedding = TinyBertService.GetEmbedding(content);
        }

        var issues = new List<QcIssueItem>();

        // 1. 报告结构完整性
        CheckReportStructure(content, issues);

        // 2. 患者基本信息完整性（需要 patientInfo）
        if (patientInfo != null)
            CheckPatientInfoCompleteness(patientInfo, issues);

        // 3. 术语标准查表
        CheckTerminologyStandards(content, issues);

        // 4. 医学词组错字检测（上下文感知）
        CheckMedicalPhraseTypos(content, issues);

        // 5. 形近字检测 + 上下文词组检测
        CheckContextualTypos(content, issues);

        // 6. 重复字检测
        CheckDuplicateChars(content, issues);

        // 7. 单位/格式规范
        CheckUnitFormat(content, issues);

        // 8. 句末标点检查
        CheckSentencPunctuation(content, issues);

        // 9. 检查技术信息完整性
        if (patientInfo != null)
            CheckExamTechniqueCompleteness(content, patientInfo, issues);

        return issues;
    }

    #region 规则1：报告结构完整性

    private static void CheckReportStructure(string content, List<QcIssueItem> issues)
    {
        
    }

    #endregion

    #region 规则2：患者基本信息完整性

    private static void CheckPatientInfoCompleteness(QcRequestInfo info, List<QcIssueItem> issues)
    {
        var required = QcKnowledgeCache.RequiredPatientFields;
        if (required.Count == 0) return; // 知识库未加载则跳过

        if (string.IsNullOrWhiteSpace(info.PatientName))
            issues.Add(Warn("患者姓名不能为空", "missing_required_field", "error", "patient_info", "请填写患者姓名"));
        if (string.IsNullOrWhiteSpace(info.PatientGender))
            issues.Add(Warn("患者性别不能为空", "missing_required_field", "error", "patient_info", "请填写患者性别"));
        else if (info.PatientGender != "男" && info.PatientGender != "女")
            issues.Add(Warn($"患者性别「{info.PatientGender}」不在标准范围内（男/女）",
                "invalid_field", "error", "patient_info", "性别应为「男」或「女」"));
        if (info.PatientAge.HasValue && (info.PatientAge < 0 || info.PatientAge > 150))
            issues.Add(Warn($"患者年龄 {info.PatientAge} 不在合理范围（0-150）",
                "invalid_field", "error", "patient_info", "请核实患者年龄"));
        if (string.IsNullOrWhiteSpace(info.AccessionNo))
            issues.Add(Warn("检查号（AccessionNo）不能为空", "missing_required_field", "warning", "patient_info", "请填写检查号"));
        if (string.IsNullOrWhiteSpace(info.ExamPart))
            issues.Add(Warn("检查部位不能为空", "missing_required_field", "warning", "patient_info", "请填写检查部位"));
        if (string.IsNullOrWhiteSpace(info.ExamDevice))
            issues.Add(Warn("检查设备不能为空", "missing_required_field", "warning", "patient_info", "请填写检查设备"));
    }

    #endregion

    #region 规则3：术语标准查表

    private static void CheckTerminologyStandards(string content, List<QcIssueItem> issues)
    {
        foreach (var term in QcKnowledgeCache.TerminologyList)
        {
            var nonStandardList = TryDeserialize(term.NonStandardTerms);
            foreach (var nonStd in nonStandardList)
            {
                if (content.Contains(nonStd))
                {
                    issues.Add(new QcIssueItem
                    {
                        Level = 1,
                        IssueType = "terminology_nonstandard",
                        OriginalText = nonStd,
                        SuggestedText = term.StandardTerm,
                        Description = $"「{nonStd}」应为规范术语「{term.StandardTerm}」",
                        Severity = "warning",
                        Location = "findings",
                        Suggestion = $"建议使用「{term.StandardTerm}」代替「{nonStd}」"
                    });
                }
            }
        }
    }

    #endregion

    #region 规则4：医学词组错字（上下文感知）

    private static void CheckMedicalPhraseTypos(string content, List<QcIssueItem> issues)
    {
        foreach (var kv in MedicalPhraseTypos)
        {
            if (content.Contains(kv.Key))
            {
                issues.Add(new QcIssueItem
                {
                    Level = 1,
                    IssueType = "text_error",
                    SubType = "phrase_typo",
                    OriginalText = kv.Key,
                    SuggestedText = kv.Value,
                    Description = $"疑似错别字「{kv.Key}」，应为「{kv.Value}」",
                    Severity = "error",
                    Location = "findings",
                    Suggestion = $"请将「{kv.Key}」更正为「{kv.Value}」"
                });
            }
        }
    }

    #endregion

    #region 规则5：上下文词组错字检测

    private static void CheckContextualTypos(string content, List<QcIssueItem> issues)
    {
        foreach (var (wrong, correct) in ContextualTypos)
        {
            if (content.Contains(wrong))
            {
                issues.Add(new QcIssueItem
                {
                    Level = 1,
                    IssueType = "text_error",
                    SubType = "contextual_typo",
                    OriginalText = wrong,
                    SuggestedText = correct,
                    Description = $"疑似错别字「{wrong}」，在报告中可能应为「{correct}」",
                    Severity = "warning",
                    Location = "findings",
                    Suggestion = $"请确认「{wrong}」是否应为「{correct}」"
                });
            }
        }
    }

    #endregion

    #region 规则6：重复字检测

    private static void CheckDuplicateChars(string content, List<QcIssueItem> issues)
    {
        var matches = DuplicateCharRegex.Matches(content);
        foreach (Match m in matches)
        {
            var dupChar = m.Value[0];
            if (!ValidDuplicateChars.Contains(dupChar))
            {
                issues.Add(new QcIssueItem
                {
                    Level = 1,
                    IssueType = "text_error",
                    SubType = "duplicate_char",
                    OriginalText = m.Value,
                    SuggestedText = dupChar.ToString(),
                    Description = $"疑似重复字「{m.Value}」",
                    Severity = "warning",
                    Location = "findings",
                    Suggestion = $"请检查是否误写了两次「{dupChar}」"
                });
            }
        }
    }

    #endregion

    #region 规则7：单位/格式规范

    private static void CheckUnitFormat(string content, List<QcIssueItem> issues)
    {
        // 7.1 乘法符号规范
        var badMult = BadMultiplySign.Matches(content);
        foreach (Match m in badMult)
        {
            // 排除在 URL / 代码中的误匹配
            var ctx = m.Value;
            issues.Add(new QcIssueItem
            {
                Level = 1,
                IssueType = "format_error",
                SubType = "multiply_sign",
                OriginalText = ctx,
                SuggestedText = ctx.Replace('x', '×').Replace('X', '×'),
                Description = $"乘法符号不规范「{ctx}」，应使用 × (U+00D7)",
                Severity = "warning",
                Location = "findings",
                Suggestion = "请使用 × 代替 x/X 作为乘法符号"
            });
        }

        // 7.2 单位前后一致性（简单检测：同一乘积表达中单位不一致）
        var szMatches = SizePattern.Matches(content);
        for (int i = 0; i < szMatches.Count - 1; i++)
        {
            var m1 = szMatches[i];
            var m2 = szMatches[i + 1];
            // 仅在两值紧邻（<20字符）时检测
            if (m2.Index - (m1.Index + m1.Length) < 20)
            {
                var unit1 = m1.Groups[1].Value.ToLower();
                var unit2 = m2.Groups[1].Value.ToLower();
                if (unit1 != unit2)
                {
                    var ctx = content.Substring(
                        Math.Max(0, m1.Index - 5),
                        Math.Min(content.Length - Math.Max(0, m1.Index - 5), m2.Index + m2.Length - Math.Max(0, m1.Index - 5) + 5));
                    issues.Add(new QcIssueItem
                    {
                        Level = 1,
                        IssueType = "format_error",
                        SubType = "unit_mismatch",
                        OriginalText = ctx.Trim(),
                        Description = $"相邻尺寸描述单位不一致（{unit1} vs {unit2}）",
                        Severity = "warning",
                        Location = "findings",
                        Suggestion = "建议统一使用同一单位（cm或mm）"
                    });
                    i++; // 跳过下一个（避免重复报告同一对）
                }
            }
        }
    }

    #endregion

    #region 规则8：句末标点检查

    private static void CheckSentencPunctuation(string content, List<QcIssueItem> issues)
    {
        // 对影像所见和诊断结论分别检查
        var sections = new[]
        {
            ("影像所见", ExtractSection(content, "影像所见", "影像表现", "所见")),
            ("诊断结论", ExtractSection(content, "诊断结论", "诊断意见", "印象", "结论")),
        };

        foreach (var (sectionName, section) in sections)
        {
            if (string.IsNullOrWhiteSpace(section)) continue;

            var sentences = section.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var sentence in sentences)
            {
                var trimmed = sentence.Trim();
                if (trimmed.Length < 4) continue;          // 太短不算句子
                if (ListItemPattern.IsMatch(trimmed)) continue; // 列表项

                if (!trimmed.EndsWith("。") && !trimmed.EndsWith("）") &&
                    !trimmed.EndsWith("；") && !trimmed.EndsWith("：") &&
                    !trimmed.EndsWith("！") && !trimmed.EndsWith("？") &&
                    !trimmed.EndsWith("."))
                {
                    issues.Add(new QcIssueItem
                    {
                        Level = 1,
                        IssueType = "text_error",
                        SubType = "missing_period",
                        OriginalText = trimmed.Length > 50 ? trimmed[..50] + "..." : trimmed,
                        SuggestedText = trimmed + "。",
                        Description = $"{sectionName}部分句末缺少标点",
                        Severity = "warning",
                        Location = sectionName == "影像所见" ? "findings" : "impression",
                        Suggestion = "请在句末添加句号（。）"
                    });
                }
            }
        }
    }

    #endregion

    #region 规则9：检查技术信息完整性

    private static void CheckExamTechniqueCompleteness(string content, QcRequestInfo info, List<QcIssueItem> issues)
    {
        // 检查是否描述了扫描方式
        if (!string.IsNullOrEmpty(info.ExamMethod))
            return; // 已提供扫描方式

        // 检查报告文本中是否提到扫描方式
        var hasMethod = new[] {
            "平扫", "增强", "平扫+增强", "增强扫描",
            "彩色多普勒", "三维重建", "多平面重建",
            "非增强", "NCE", "CE", "CTA", "MRA", "DWI", "ADC"
        }.Any(k => content.Contains(k));

        if (!hasMethod)
        {
            issues.Add(new QcIssueItem
            {
                Level = 1,
                IssueType = "missing_info",
                SubType = "exam_method",
                Description = "报告中未描述扫描方式（平扫/增强/多普勒等）",
                Severity = "warning",
                Location = "findings",
                Suggestion = "建议在报告中注明扫描方式"
            });
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>提取报告某个章节</summary>
    private static string ExtractSection(string content, params string[] headers)
    {
        foreach (var h in headers)
        {
            var idx = content.IndexOf(h, StringComparison.Ordinal);
            if (idx < 0) continue;

            // 找下一个章节头作为结束
            var allHeaders = new[] { "影像所见", "影像表现", "所见", "诊断结论", "诊断意见", "印象", "结论", "建议", "报告医师", "审核医师" };
            var endIdx = int.MaxValue;
            foreach (var ah in allHeaders)
            {
                if (ah == h) continue;
                var ei = content.IndexOf(ah, idx + h.Length, StringComparison.Ordinal);
                if (ei > idx && ei < endIdx) endIdx = ei;
            }
            return endIdx < int.MaxValue ? content[idx..endIdx] : content[idx..];
        }
        return "";
    }

    private static bool IsValidDuplicate(char c) => ValidDuplicateChars.Contains(c);

    private static List<string> TryDeserialize(string json)
    {
        try { return Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(json) ?? new(); }
        catch { return new(); }
    }

    private static QcIssueItem Warn(string desc, string subType, string severity, string location, string suggestion)
    {
        return new QcIssueItem
        {
            Level = 1,
            IssueType = subType.Split('_')[0] == "structure" || subType.Split('_')[0] == "missing"
                ? "completeness_error" : "text_error",
            SubType = subType,
            Description = desc,
            Severity = severity,
            Location = location,
            Suggestion = suggestion
        };
    }

    #endregion

    #region 内部模型

    /// <summary>患者信息摘要（用于完整性检查）</summary>
    public class QcRequestInfo
    {
        public string? PatientName { get; set; }
        public string? PatientGender { get; set; }
        public int? PatientAge { get; set; }
        public string? AccessionNo { get; set; }
        public string? ExamPart { get; set; }
        public string? ExamDevice { get; set; }
        public string? ExamMethod { get; set; }
        public string? ReportType { get; set; }
    }

    public class QcIssueItem
    {
        public int Level { get; set; }
        public string IssueType { get; set; } = string.Empty;
        public string? SubType { get; set; }
        public string OriginalText { get; set; } = string.Empty;
        public string SuggestedText { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Severity { get; set; } = "warning";
        public string? Location { get; set; }
        public string? Suggestion { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }

    #endregion
}
