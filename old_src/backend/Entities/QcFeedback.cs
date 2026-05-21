// 质控反馈表 QcFeedback — 对应 qc_feedback 表
// 记录医生对质控结果的确认/修正意见，用于模型迭代训练

namespace ReportQC.Entities;

public class QcFeedback : JSBaseDBEntity
{
    /// <summary>关联质控记录 ID</summary>
    public long QcReportId { get; set; }

    /// <summary>关联质控问题 ID（0 表示对整份报告的综合反馈）</summary>
    public long IssueId { get; set; }

    /// <summary>医生操作：accepted / rejected / modified / supplement</summary>
    public string Action { get; set; } = "accepted";

    /// <summary>医生意见/说明</summary>
    public string? Comment { get; set; }

    /// <summary>医生修正后的文本（action=modified 时填写）</summary>
    public string? CorrectedText { get; set; }

    /// <summary>医生补充发现的问题</summary>
    public string? SupplementIssues { get; set; }

    /// <summary>操作医生</summary>
    public string? DoctorName { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; }
}
