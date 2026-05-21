// Level2QcService — 语义层质控（术语规范、RADS 分类、描述规范性、所见-结论一致性）
// 基于《放射诊断报告书写规范专家共识》（2025）& DB11/T 2505—2025
// 三层架构：口语化检测 → 术语规范 → 语义一致性
// 当 TinyBERT 模型就绪时自动切换为 ONNX 推理

using System.Text.RegularExpressions;
using ReportQC.Entities;

namespace ReportQC.Services;

public static class Level2QcService
{
    #region 口语化/不规范表达检测（50+ 条目）

    private static readonly (string word, string suggestion)[] ColloquialTerms =
    {
        ("有点", "建议使用精确描述"),
        ("还好", "建议使用精确描述"),
        ("看起来", "建议使用客观描述"),
        ("似乎", "建议使用确定语气"),
        ("好像", "建议使用确定语气"),
        ("不太", "建议使用明确描述"),
        ("比较大", "建议标注具体尺寸"),
        ("比较小", "建议标注具体尺寸"),
        ("若干个", "建议标注具体数量"),
        ("好几个", "建议标注具体数量"),
        ("基本上", "建议使用精确描述"),
        ("差不多", "建议使用精确描述"),
        ("大概", "建议标注具体数值"),
        ("也许", "建议使用确定语气"),
        ("估计", "建议使用确定语气"),
        ("看不清楚", "建议明确描述为「显示欠清」"),
        ("看不太清", "建议使用「显示欠清」"),
        ("隐约可见", "建议使用「隐约显示」"),
        ("一般般", "建议使用精确描述"),
        ("一大片", "建议使用精确尺寸"),
        ("一点点", "建议使用精确尺寸"),
        ("有些", "建议使用明确描述"),
        ("比较", "建议使用比较级精确描述"),
        ("这个", "建议使用医学名词代替指示代词"),
        ("那个", "建议使用医学名词代替指示代词"),
        ("这边", "建议使用解剖方位"),
        ("那边", "建议使用解剖方位"),
        ("还行", "建议使用精确描述"),
        ("挺", "建议使用精确程度词"),
        ("非常", "建议使用精确程度词"),
        ("特别", "建议使用精确描述"),
        ("很", "建议使用量化描述"),
        ("不少", "建议标注具体数量"),
        ("一部分", "建议标注具体描述"),
        ("两边", "建议使用「双侧」"),
        ("上面", "建议使用「上方」或具体解剖方位"),
        ("下面", "建议使用「下方」或具体解剖方位"),
        ("旁边", "建议使用具体解剖方位"),
        ("周围一圈", "建议使用「环绕」或「周围」"),
        ("中间", "建议使用「中央」或「中心」"),
        ("越来越", "建议使用「进行性」"),
        ("总算", "非医学用语，建议删除"),
        ("目前看来", "建议使用「目前」"),
        ("大体上", "建议使用「大体」"),
        ("不怎么", "建议使用明确描述"),
        ("感觉", "非客观用语，建议使用「显示/提示」"),
        ("看上去", "建议使用「显示」"),
        ("可见到", "建议使用「可见」（冗余表达）"),
        ("可以见到", "建议使用「可见」"),
        ("我们认为", "建议删除（报告中不应出现主观表述）"),
        ("据观察", "建议删除"),
        ("应该说", "非医学用语"),
        ("相当", "建议使用明确的量化描述"),
    };

    #endregion

    #region 术语规范性 — 解剖命名规范（80+ 条目）

    private static readonly Dictionary<string, string> AnatomyStandardTerms = new()
    {
        { "右上肺", "右肺上叶" }, { "右下肺", "右肺下叶" },
        { "右肺上部", "右肺上叶" }, { "右肺下部", "右肺下叶" },
        { "右中肺", "右肺中叶" }, { "右肺中部", "右肺中叶" },
        { "左上肺", "左肺上叶" }, { "左下肺", "左肺下叶" },
        { "左肺上部", "左肺上叶" }, { "左肺下部", "左肺下叶" },
        { "右上叶", "右肺上叶" }, { "右下叶", "右肺下叶" },
        { "左上叶", "左肺上叶" }, { "左下叶", "左肺下叶" },

        { "肝右", "肝右叶" }, { "肝左", "肝左叶" },
        { "右肝", "肝右叶" }, { "左肝", "肝左叶" },
        { "右半肝", "肝右叶" }, { "左半肝", "肝左叶" },
        { "肝脏右叶", "肝右叶" }, { "肝脏左叶", "肝左叶" },

        { "甲状腺左", "甲状腺左叶" }, { "甲状腺右", "甲状腺右叶" },
        { "左甲", "甲状腺左叶" }, { "右甲", "甲状腺右叶" },
        { "左侧甲状腺", "甲状腺左叶" }, { "右侧甲状腺", "甲状腺右叶" },

        { "左肾", "左肾" }, { "右肾", "右肾" },
        { "左子宫", "子宫左侧" }, { "右子宫", "子宫右侧" },
        { "左附件区", "左侧附件区" }, { "右附件区", "右侧附件区" },

        { "头颅", "颅脑" }, { "脑部", "颅脑" },
        { "脑内", "颅内" },

        { "胸腔内", "胸腔" }, { "腹腔内", "腹腔" },
        { "盆腔内", "盆腔" },

        { "脊椎", "脊柱" }, { "脊梁", "脊柱" },
        { "颈椎骨", "颈椎" }, { "腰椎骨", "腰椎" },

        { "膝关节内部", "膝关节" }, { "肩关节内部", "肩关节" },
        { "髋关节内部", "髋关节" },

        { "甲状腺部位", "甲状腺" }, { "乳腺部位", "乳腺" },
        { "前列腺部位", "前列腺" }, { "前列腺区", "前列腺" },

        { "胆部", "胆囊" }, { "胰部", "胰腺" },
        { "脾部", "脾脏" }, { "胃部", "胃" },
        { "肠部", "肠道" },

        { "血管内部", "血管腔" }, { "血管内", "血管腔" },
        { "管腔内", "管腔" },
    };

    #endregion

    #region 病灶描述完整性关键词

    private static readonly string[] SizeKeywords =
        { "大小", "直径", "约", "cm", "mm", "长径", "短径", "半径", "体积" };

    private static readonly string[] MorphologyKeywords =
    {
        "形态", "形状", "边界", "边缘", "密度", "信号", "回声",
        "增强", "强化", "钙化", "坏死", "囊变", "出血", "水肿",
        "分叶", "毛刺", "光滑", "不规则", "清晰", "模糊",
        "圆形", "椭圆形", "类圆形", "不规则形",
        "均匀", "不均匀", "混杂",
    };

    private static readonly string[] LesionKeywords =
    {
        "结节", "肿块", "占位", "病灶", "阴影", "囊肿", "瘤",
        "癌", "占位性病变", "异常密度", "异常信号", "异常回声",
        "实质性", "囊性", "混合性"
    };

    #endregion

    #region 所见-结论一致性

    private static readonly string[] FullNegationPhrases =
    {
        "未见异常", "未见明显异常", "未见明确异常",
        "未见明显器质性异常", "正常", "无异常发现",
        "未见明显病变", "未见确切异常"
    };

    private static readonly string[] ImpressionHeaders =
    {
        "诊断结论", "诊断意见", "印象", "结论"
    };

    #endregion

    // ── RADS 类型映射（根据报告类型查找应使用的 RADS 分类） ──
    private static readonly Dictionary<string, string> RadsTypeMap = new()
    {
        { "乳腺", "BI-RADS" }, { "钼靶", "BI-RADS" }, { "乳腺超声", "BI-RADS" },
        { "肝脏", "LI-RADS" }, { "肝脏MRI", "LI-RADS" },
        { "甲状腺", "TI-RADS" }, { "甲状腺超声", "TI-RADS" },
        { "前列腺", "PI-RADS" }, { "前列腺MRI", "PI-RADS" },
        { "肺部", "Lung-RADS" }, { "肺结节", "Lung-RADS" }, { "胸部CT筛查", "Lung-RADS" },
    };

    /// <summary>
    /// 执行 Level 2 语义层质控
    /// </summary>
    public static List<QcIssueItem> Run(string fullContent, string findings, string impression, string? reportType)
    {
        if (TinyBertService.IsLoaded)
        {
            var embedding = TinyBertService.GetEmbedding(fullContent);
        }

        var issues = new List<QcIssueItem>();

        // 1. 口语化描述检测
        CheckColloquialTerms(fullContent, issues);

        // 2. 解剖命名规范
        CheckAnatomyTerms(fullContent, issues);

        // 3. 病灶描述完整性
        CheckLesionCompleteness(findings, issues);

        // 4. 量化描述要求
        CheckQuantitativeDescription(findings, issues);

        // 5. 否定词位置规范
        CheckNegationPosition(findings, issues);

        // 6. RADS 分类检查（根据报告类型）
        if (!string.IsNullOrEmpty(reportType))
            CheckRadsClassification(impression, reportType, issues);

        // 7. 所见-结论语义一致性（句子级检测）
        var consistencyIssues = CheckFindingsImpressionConsistency(findings, impression);
        issues.AddRange(consistencyIssues);

        // 8. 对比描述规范性
        CheckComparisonDescription(findings, issues);

        // 9. 建议与诊断一致性
        CheckAdviceConsistency(impression, issues);

        // 去重
        issues = DeduplicateIssues(issues);

        return issues;
    }

    #region 规则1：口语化检测

    private static void CheckColloquialTerms(string content, List<QcIssueItem> issues)
    {
        foreach (var (word, suggestion) in ColloquialTerms)
        {
            if (content.Contains(word))
            {
                issues.Add(new QcIssueItem
                {
                    Level = 2,
                    IssueType = "terminology_error",
                    SubType = "colloquial",
                    OriginalText = word,
                    Description = $"报告中出现口语化/非客观描述「{word}」",
                    Severity = "warning",
                    Location = "findings",
                    Suggestion = suggestion
                });
            }
        }
    }

    #endregion

    #region 规则2：解剖命名规范

    private static void CheckAnatomyTerms(string content, List<QcIssueItem> issues)
    {
        foreach (var kv in AnatomyStandardTerms)
        {
            if (content.Contains(kv.Key))
            {
                issues.Add(new QcIssueItem
                {
                    Level = 2,
                    IssueType = "terminology_error",
                    SubType = "anatomy_nonstandard",
                    OriginalText = kv.Key,
                    SuggestedText = kv.Value,
                    Description = $"解剖术语不标准「{kv.Key}」，建议使用「{kv.Value}」",
                    Severity = "warning",
                    Location = "findings",
                    Suggestion = $"请使用国际通用解剖命名「{kv.Value}」代替「{kv.Key}」"
                });
            }
        }
    }

    #endregion

    #region 规则3：病灶描述完整性

    private static void CheckLesionCompleteness(string findings, List<QcIssueItem> issues)
    {
        var hasLesion = LesionKeywords.Any(k => findings.Contains(k));
        if (!hasLesion) return;

        // 3.1 尺寸检查
        var hasSize = SizeKeywords.Any(k => findings.Contains(k));
        if (!hasSize)
        {
            issues.Add(new QcIssueItem
            {
                Level = 2,
                IssueType = "terminology_error",
                SubType = "missing_size",
                Description = "病灶描述缺少精确尺寸（如「大小约 1.5cm」）",
                Severity = "error",
                Location = "findings",
                Suggestion = "所有可测量的病变必须标注具体大小和单位"
            });
        }

        // 3.2 形态特征检查
        var morphologyFound = MorphologyKeywords.Where(k => findings.Contains(k)).ToList();
        if (morphologyFound.Count < 3)
        {
            issues.Add(new QcIssueItem
            {
                Level = 2,
                IssueType = "terminology_error",
                SubType = "incomplete_morphology",
                Description = $"病灶形态描述不完整（仅描述了{morphologyFound.Count}个特征：{string.Join("、", morphologyFound.DefaultIfEmpty("无"))}），建议补充形态/边界/密度/信号等特征",
                Severity = "warning",
                Location = "findings",
                Suggestion = "请补充病灶的形态、边界、密度/信号、增强特征等描述"
            });
        }
    }

    #endregion

    #region 规则4：量化描述要求

    private static void CheckQuantitativeDescription(string findings, List<QcIssueItem> issues)
    {
        // 检查是否有"多发性"描述但未标注具体数量
        if (findings.Contains("多发性") || findings.Contains("多发") ||
            findings.Contains("数个") || findings.Contains("若干"))
        {
            var hasCount = Regex.IsMatch(findings, @"(\d+个|\d+枚|\d+处|共\d+|约\d+)");
            if (!hasCount)
            {
                issues.Add(new QcIssueItem
                {
                    Level = 2,
                    IssueType = "terminology_error",
                    SubType = "missing_count",
                    Description = "使用「多发/数个」但未标注具体数量，建议明确数量",
                    Severity = "warning",
                    Location = "findings",
                    Suggestion = "请使用「N个/N枚/N处」等精确数量描述"
                });
            }
        }
    }

    #endregion

    #region 规则5：否定词位置规范

    private static void CheckNegationPosition(string findings, List<QcIssueItem> issues)
    {
        // 检查"未见"是否出现在句中而非句首
        // 模式：非句首位置出现"未见"
        var sentences = findings.Split(new[] { '。', '；', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (trimmed.Length < 6) continue;

            var weiIdx = trimmed.IndexOf("未见", StringComparison.Ordinal);
            // "未见"不在句首（非前2个字符），且前面不是"均"/"亦"/"也"等副词
            if (weiIdx > 2 && !trimmed[..weiIdx].EndsWith("均") &&
                !trimmed[..weiIdx].EndsWith("亦") && !trimmed[..weiIdx].EndsWith("也"))
            {
                issues.Add(new QcIssueItem
                {
                    Level = 2,
                    IssueType = "terminology_error",
                    SubType = "negation_position",
                    OriginalText = trimmed.Length > 40 ? trimmed[..40] + "..." : trimmed,
                    Description = $"「未见」应置于句首（当前在句中位置{weiIdx}），如「{trimmed.Replace("未见", "【未见】")}」",
                    Severity = "warning",
                    Location = "findings",
                    Suggestion = "否定词「未见」应置于句首，避免「xxx未见xxx」的表述"
                });
                break; // 仅报告第一个发现
            }
        }
    }

    #endregion

    #region 规则6：RADS 分类检查

    private static void CheckRadsClassification(string impression, string reportType, List<QcIssueItem> issues)
    {
        // 首先通过 RadsTypeMap 精确查找
        foreach (var kv in RadsTypeMap)
        {
            if (reportType.Contains(kv.Key))
            {
                var radsName = kv.Value;
                if (!impression.Contains(radsName, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new QcIssueItem
                    {
                        Level = 2,
                        IssueType = "rads_missing",
                        Description = $"{reportType} 报告应在结论中标注 {radsName} 分级",
                        Severity = "error",
                        Location = "impression",
                        Suggestion = $"请在诊断结论中补充 {radsName} 分级"
                    });
                }
                return; // 找到映射后不再继续
            }
        }

        // 兜底：使用知识库中的 RadsRequireTypes
        foreach (var kv in QcKnowledgeCache.RadsRequireTypes)
        {
            if (reportType.Contains(kv.Key))
            {
                foreach (var rads in kv.Value)
                {
                    if (!impression.Contains(rads, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new QcIssueItem
                        {
                            Level = 2,
                            IssueType = "rads_missing",
                            Description = $"{reportType} 报告应标注 {rads} 分级",
                            Severity = "error",
                            Location = "impression",
                            Suggestion = $"请补充 {rads} 分级"
                        });
                    }
                }
            }
        }
    }

    #endregion

    #region 规则7：所见-结论语义一致性（句子级）

    private static List<QcIssueItem> CheckFindingsImpressionConsistency(string findings, string impression)
    {
        var issues = new List<QcIssueItem>();

        if (string.IsNullOrWhiteSpace(findings) || string.IsNullOrWhiteSpace(impression))
            return issues;

        // ── TinyBERT 语义相似度 ──
        if (TinyBertService.IsLoaded)
        {
            var findingsEmb = TinyBertService.GetEmbedding(findings);
            var impressionEmb = TinyBertService.GetEmbedding(impression);

            if (findingsEmb != null && impressionEmb != null)
            {
                var similarity = TinyBertService.CosineSimilarity(findingsEmb, impressionEmb);

                if (similarity < 0.35f)
                {
                    issues.Add(new QcIssueItem
                    {
                        Level = 2, IssueType = "semantic_conflict", SubType = "low_similarity",
                        Description = $"所见与诊断结论语义相似度过低（{similarity:F2}），可能描述的不是同一检查结果",
                        Severity = "error", Location = "whole",
                        Suggestion = "请检查所见描述与诊断结论是否对应"
                    });
                }
                if (similarity is >= 0.35f and < 0.55f)
                {
                    issues.Add(new QcIssueItem
                    {
                        Level = 2, IssueType = "semantic_conflict", SubType = "moderate_similarity",
                        Description = $"所见与诊断结论语义一致性偏低（{similarity:F2}），建议核对",
                        Severity = "warning", Location = "whole",
                        Suggestion = "请确认诊断结论是否基于所见描述"
                    });
                }
            }
        }

        // ── 句子级阴阳性矛盾检测 ──
        // 提取所见最后一小段（通常是最重要的结论性描述）
        var findingsLastPart = GetLastMeaningfulSegment(findings);
        // 提取结论第一小段（通常是最关键的诊断）
        var impressionFirstPart = GetFirstMeaningfulSegment(impression);

        // 所见全阴性 + 结论阳性 → 矛盾
        var findingsAllNegative = FullNegationPhrases.Any(p => findingsLastPart.Contains(p));
        var impressionHasPositiveDiag = QcKnowledgeCache.PositiveDiagnosisWords.Any(
            w => impressionFirstPart.Contains(w));

        if (findingsAllNegative && impressionHasPositiveDiag)
        {
            issues.Add(new QcIssueItem
            {
                Level = 2, IssueType = "semantic_conflict", SubType = "diagnosis_jump",
                Description = "所见描述均为阴性（正常），但结论给出阳性诊断",
                Severity = "error", Location = "whole",
                Suggestion = "请检查所见描述是否遗漏了阳性发现"
            });
        }

        // ── 方位矛盾（句子级） ──
        CheckDirectionConsistency(findingsLastPart, impressionFirstPart, issues);

        // ── 程度矛盾（句子级） ──
        CheckSeverityConsistency(findingsLastPart, impressionFirstPart, issues);

        return issues;
    }

    private static string GetLastMeaningfulSegment(string text)
    {
        // 取最后 200 个字符（覆盖大部分结论性描述）
        return text.Length > 200 ? text[^200..] : text;
    }

    private static string GetFirstMeaningfulSegment(string text)
    {
        // 移除标题行后取前 200 个字符
        foreach (var h in ImpressionHeaders)
            text = text.Replace(h, "");
        text = text.Trim();
        return text.Length > 200 ? text[..200] : text;
    }

    // ── 方位一致性（句子级） ──
    private static void CheckDirectionConsistency(string findingsPart, string impressionPart, List<QcIssueItem> issues)
    {
        var directionPairs = new Dictionary<string, string>
        {
            { "左侧", "右侧" }, { "左叶", "右叶" },
            { "左肺", "右肺" }, { "左肾", "右肾" },
            { "左上", "右上" }, { "左下", "右下" },
            { "左前", "右前" }, { "左后", "右后" },
        };

        foreach (var kv in directionPairs)
        {
            // 仅在"所见"中出现某侧 + "结论"中出现另一侧时才报警
            if (findingsPart.Contains(kv.Key) && impressionPart.Contains(kv.Value))
            {
                issues.Add(new QcIssueItem
                {
                    Level = 2, IssueType = "semantic_conflict", SubType = "direction_conflict",
                    Description = $"所见描述涉及「{kv.Key}」但结论涉及「{kv.Value}」，方位矛盾",
                    Severity = "error", Location = "whole",
                    Suggestion = $"请确认「{kv.Key}」与「{kv.Value}」方位描述是否一致"
                });
            }
        }
    }

    // ── 程度一致性 ──
    private static void CheckSeverityConsistency(string findingsPart, string impressionPart, List<QcIssueItem> issues)
    {
        var severityPairs = new (string mild, string severe)[]
        {
            ("轻度", "重度"), ("轻微", "重度"), ("少量", "大量"),
            ("少许", "大量"), ("不明显", "明显"), ("轻度", "明显"),
            ("轻度", "严重"), ("轻微", "严重"), ("不明显", "显著"),
            ("轻度", "广泛"), ("局限", "弥漫"),
        };

        foreach (var (mild, severe) in severityPairs)
        {
            if (findingsPart.Contains(mild) && impressionPart.Contains(severe))
            {
                issues.Add(new QcIssueItem
                {
                    Level = 2, IssueType = "semantic_conflict", SubType = "severity_conflict",
                    Description = $"所见描述为「{mild}」但结论描述为「{severe}」，程度矛盾",
                    Severity = "error", Location = "whole",
                    Suggestion = "请确认所见与结论中的程度描述是否一致"
                });
            }
        }
    }

    #endregion

    #region 规则8：对比描述规范性

    private static void CheckComparisonDescription(string findings, List<QcIssueItem> issues)
    {
        var compareWords = new[] { "与前片比较", "与既往比较", "与前次比较", "对比前片", "对照前次", "与前片相比" };
        var compareResultWords = new[] { "无明显变化", "增大", "缩小", "增多", "减少", "好转", "加重", "稳定", "大致同前", "基本同前", "相仿", "相似", "进展", "消退", "吸收" };

        foreach (var cw in compareWords)
        {
            if (!findings.Contains(cw)) continue;

            var cwIdx = findings.IndexOf(cw, StringComparison.Ordinal);
            // 检查比较词后 100 字符内是否有比较结论
            var afterCompare = cwIdx + cw.Length < findings.Length
                ? findings.Substring(cwIdx + cw.Length, Math.Min(100, findings.Length - cwIdx - cw.Length))
                : "";

            var hasResult = compareResultWords.Any(r => afterCompare.Contains(r));
            if (!hasResult)
            {
                issues.Add(new QcIssueItem
                {
                    Level = 2,
                    IssueType = "terminology_error",
                    SubType = "missing_comparison_result",
                    OriginalText = cw,
                    Description = $"描述了与既往检查比较（「{cw}」），但缺少比较结论（如「无明显变化」「增大」「缩小」等）",
                    Severity = "warning",
                    Location = "findings",
                    Suggestion = "请在比较描述后补充明确的比较结论"
                });
                break; // 仅报告一次
            }
        }
    }

    #endregion

    #region 规则9：建议与诊断一致性

    private static void CheckAdviceConsistency(string impression, List<QcIssueItem> issues)
    {
        // 9.1 有阳性/可疑诊断但缺少随访建议
        var hasSuspicious = new[] { "考虑", "可疑", "不除外", "恶性", "占位", "建议活检", "性质待定" }
            .Any(k => impression.Contains(k));
        var hasFollowup = QcKnowledgeCache.FollowupIndicators.Any(k => impression.Contains(k));

        if (hasSuspicious && !hasFollowup)
        {
            issues.Add(new QcIssueItem
            {
                Level = 2,
                IssueType = "terminology_error",
                SubType = "missing_followup",
                Description = "诊断结论涉及可疑/占位性病变，但缺少随访或进一步检查建议",
                Severity = "warning",
                Location = "impression",
                Suggestion = "请根据诊断结论补充随访建议或进一步检查方案"
            });
        }

        // 9.2 诊断明确良性但有过度检查建议（信息性提示）
        var hasBenign = new[] { "良性", "正常", "未见异常", "未见明确异常" }
            .Any(k => impression.Contains(k));
        var hasOverCheck = new[] { "建议活检", "建议穿刺", "建议手术" }
            .Any(k => impression.Contains(k));

        if (hasBenign && hasOverCheck)
        {
            issues.Add(new QcIssueItem
            {
                Level = 2,
                IssueType = "terminology_error",
                SubType = "over_check_advice",
                Description = "诊断结论为良性/正常，但建议中包含了活检/穿刺/手术等过度检查措施",
                Severity = "warning",
                Location = "impression",
                Suggestion = "请确认诊断结论与建议是否匹配"
            });
        }
    }

    #endregion

    #region 去重 & 辅助

    private static List<QcIssueItem> DeduplicateIssues(List<QcIssueItem> items)
    {
        var seen = new HashSet<string>();
        var result = new List<QcIssueItem>();
        foreach (var item in items)
        {
            var key = $"{item.IssueType}|{item.SubType ?? ""}|{item.Description ?? ""}";
            if (seen.Add(key))
                result.Add(item);
        }
        return result;
    }

    #endregion

    #region 内部模型

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
