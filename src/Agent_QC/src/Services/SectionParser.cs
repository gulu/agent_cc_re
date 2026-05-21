namespace Agent_QC.Services;

/// <summary>
/// 放射科报告段落解析器。
/// 将报告全文拆分为：临床信息(ClinicalHistory)、影像所见(Findings)、
/// 诊断意见(Impression)、随访建议(Recommendation)。
/// </summary>
public class SectionParser
{
    /// <summary>
    /// 段落头关键词，按优先级排列。匹配到第一个关键词后，后续内容归入该段落。
    /// </summary>
    private static readonly (string Header, string Field)[] Headers = new[]
    {
        // Findings field
        ("影像学表现", nameof(ReportSections.Findings)),
        ("影像表现",   nameof(ReportSections.Findings)),
        ("影像所见",   nameof(ReportSections.Findings)),
        ("检查所见",   nameof(ReportSections.Findings)),
        ("所见",       nameof(ReportSections.Findings)),

        // Impression field
        ("诊断意见",   nameof(ReportSections.Impression)),
        ("诊断结论",   nameof(ReportSections.Impression)),
        ("印象",       nameof(ReportSections.Impression)),
        ("诊断",       nameof(ReportSections.Impression)),

        // ClinicalHistory field
        ("临床诊断",   nameof(ReportSections.ClinicalHistory)),
        ("主诉",       nameof(ReportSections.ClinicalHistory)),
        ("病史",       nameof(ReportSections.ClinicalHistory)),

        // Recommendation field
        ("建议",       nameof(ReportSections.Recommendation)),
        ("随访建议",   nameof(ReportSections.Recommendation)),
    };

    public ReportSections Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ReportSections();

        var sections = new ReportSections();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // 当前活跃段落：Findings为默认段落（无明确段落头时内容归入Findings）
        string currentField = nameof(ReportSections.Findings);
        var currentContent = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // 检查是否为段落头行（如 "影像所见："、"诊断意见："）
            var matchedField = TryMatchHeader(trimmed);
            if (matchedField != null)
            {
                // 保存上一个段落的内容
                AppendToSection(sections, currentField, currentContent);
                currentContent.Clear();
                currentField = matchedField;

                // 提取段落头冒号后的内容（如有）
                var afterColon = ExtractAfterColon(trimmed);
                if (!string.IsNullOrEmpty(afterColon))
                    currentContent.Add(afterColon);
            }
            else
            {
                currentContent.Add(trimmed);
            }
        }

        // 保存最后一个段落
        AppendToSection(sections, currentField, currentContent);

        return sections;
    }

    /// <summary>尝试匹配段落头关键词，返回对应的字段名或null。</summary>
    private static string? TryMatchHeader(string line)
    {
        foreach (var (header, field) in Headers)
        {
            if (line.StartsWith(header))
                return field;
        }
        return null;
    }

    /// <summary>提取冒号后的文本（如 "影像所见：双肺清晰" → "双肺清晰"）。</summary>
    private static string? ExtractAfterColon(string line)
    {
        var idx = line.IndexOfAny(new[] { '：', ':' });
        if (idx < 0 || idx >= line.Length - 1) return null;

        var after = line[(idx + 1)..].Trim();
        return after.Length > 0 ? after : null;
    }

    /// <summary>将收集到的行追加到对应段落。</summary>
    private static void AppendToSection(ReportSections sections, string field, List<string> lines)
    {
        if (lines.Count == 0) return;
        var content = string.Join("\n", lines);

        switch (field)
        {
            case nameof(ReportSections.Findings):
                sections.Findings = Concat(sections.Findings, content); break;
            case nameof(ReportSections.Impression):
                sections.Impression = Concat(sections.Impression, content); break;
            case nameof(ReportSections.ClinicalHistory):
                sections.ClinicalHistory = Concat(sections.ClinicalHistory, content); break;
            case nameof(ReportSections.Recommendation):
                sections.Recommendation = Concat(sections.Recommendation, content); break;
        }
    }

    private static string Concat(string existing, string addition)
        => string.IsNullOrEmpty(existing) ? addition : existing + "\n" + addition;
}

/// <summary>报告段落容器。</summary>
public class ReportSections
{
    public string ClinicalHistory { get; set; } = string.Empty;
    public string Findings { get; set; } = string.Empty;
    public string Impression { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
