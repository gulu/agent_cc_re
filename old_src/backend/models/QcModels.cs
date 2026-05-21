// 质控请求/响应 DTO
// 包含完整的报告信息、患者信息、临床诊断信息、检查申请信息

using System.Text.RegularExpressions;

namespace ReportQC.Models;

/// <summary>质控请求（完整 JSON 传入）</summary>
public class QcRequest
{
    // ── 报告信息 ──────────────────────────────────

    /// <summary>报告 ID（RIS 系统传入），格式：RIS-YYYYMMDD-NNNNN</summary>
    public string ReportId { get; set; } = string.Empty;

    /// <summary>所见描述（影像所见部分）</summary>
    public string Findings { get; set; } = string.Empty;

    /// <summary>诊断结论（印象/结论部分）</summary>
    public string Impression { get; set; } = string.Empty;

    /// <summary>
    /// 报告文本全文（兼容旧接口，自动拆分为 Findings + Impression）
    /// 设置此属性时自动按分隔关键字拆分
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string ReportContent
    {
        set
        {
            if (string.IsNullOrEmpty(value)) return;
            var splitKeywords = new[] { "诊断结论", "诊断意见", "印象", "结论" };
            var idx = -1;
            foreach (var kw in splitKeywords)
            {
                idx = value.IndexOf(kw, StringComparison.Ordinal);
                if (idx > 0) break;
            }
            if (idx > 0)
            {
                Findings = value[..idx].Trim();
                Impression = value[idx..].Trim();
            }
            else
            {
                Findings = value;
                Impression = value;
            }
        }
    }

    /// <summary>报告文本全文（Findings + Impression 组合）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string FullContent => string.IsNullOrEmpty(Impression) ? Findings : $"{Findings}\n{Impression}";

    /// <summary>报告类型：CT / MRI / DR / 超声 / 内镜 / 钼靶 / DSA</summary>
    public string? ReportType { get; set; }

    // ── 患者信息 ──────────────────────────────────

    /// <summary>患者姓名</summary>
    public string? PatientName { get; set; }

    /// <summary>患者性别：男 / 女</summary>
    public string? PatientGender { get; set; }

    /// <summary>患者年龄（岁）</summary>
    public int? PatientAge { get; set; }

    /// <summary>患者身份证号（脱敏，仅后4位）</summary>
    public string? PatientIdNo { get; set; }

    /// <summary>门诊号</summary>
    public string? OutpatientNo { get; set; }

    /// <summary>住院号</summary>
    public string? InpatientNo { get; set; }

    /// <summary>患者电话（脱敏）</summary>
    public string? PatientPhone { get; set; }

    // ── 临床信息 ──────────────────────────────────

    /// <summary>临床诊断</summary>
    public string? ClinicalDiagnosis { get; set; }

    /// <summary>临床症状</summary>
    public string? Symptoms { get; set; }

    /// <summary>病史摘要</summary>
    public string? MedicalHistory { get; set; }

    /// <summary>申请科室</summary>
    public string? RequestDepartment { get; set; }

    /// <summary>申请医生</summary>
    public string? RequestDoctor { get; set; }

    // ── 检查信息 ──────────────────────────────────

    /// <summary>检查部位</summary>
    public string? ExamPart { get; set; }

    /// <summary>检查设备：CT / MRI / DR / 超声 / 胃镜 / 结肠镜 / 钼靶 / DSA</summary>
    public string? ExamDevice { get; set; }

    /// <summary>检查方法：平扫 / 增强 / 平扫+增强 / 彩色多普勒 / 三维重建</summary>
    public string? ExamMethod { get; set; }

    /// <summary>申请单号</summary>
    public string? RequestNo { get; set; }

    /// <summary>检查号（Accession Number）</summary>
    public string? AccessionNo { get; set; }

    /// <summary>检查日期（yyyy-MM-dd）</summary>
    public string? ExamDate { get; set; }

    /// <summary>报告日期（yyyy-MM-dd）</summary>
    public string? ReportDate { get; set; }

    // ── 校验 ──────────────────────────────────────

    /// <summary>校验 ReportId 格式（非空且不为纯空格）</summary>
    public bool ValidateReportId(out string error)
    {
        if (string.IsNullOrWhiteSpace(ReportId))
        {
            error = "reportId 不能为空";
            return false;
        }
        if (ReportId.Length > 64)
        {
            error = "reportId 长度不能超过64个字符";
            return false;
        }
        error = string.Empty;
        return true;
    }

    /// <summary>校验报告内容</summary>
    public bool ValidateContent(out string error)
    {
        var full = FullContent;
        if (string.IsNullOrWhiteSpace(full))
        {
            error = "findings 或 impression 不能为空";
            return false;
        }
        if (full.Length < 10)
        {
            error = "报告内容过短（至少10个字符）";
            return false;
        }
        if (full.Length > 50000)
        {
            error = "报告内容过长（最多50000个字符）";
            return false;
        }
        error = string.Empty;
        return true;
    }
}

/// <summary>评分维度检查结果</summary>
public class QcCheckItem
{
    public string DimensionCode { get; set; } = string.Empty;
    public string DimensionName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public decimal Score { get; set; }
    public decimal Weight { get; set; }
}

/// <summary>质控响应 DTO</summary>
public class QcResponse
{
    public string ReportId { get; set; } = string.Empty;
    public decimal TotalScore { get; set; }
    public decimal PassScore { get; set; } = 90;
    public bool Passed { get; set; }
    public string QcLevel { get; set; } = string.Empty;
    public List<QcCheckItem> CheckItems { get; set; } = new();
    public List<QcIssueDto> Issues { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public int ProcessTimeMs { get; set; }
}

/// <summary>质控问题 DTO</summary>
public class QcIssueDto
{
    public string IssueType { get; set; } = string.Empty;
    public string? SubType { get; set; }
    public string? Description { get; set; }
    public string Severity { get; set; } = "warning";
    public string? Location { get; set; }
    public string? OriginalText { get; set; }
    public string? SuggestedText { get; set; }
    public string? Suggestion { get; set; }
}
