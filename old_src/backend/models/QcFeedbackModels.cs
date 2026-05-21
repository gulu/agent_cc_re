// 质控反馈请求 DTO

namespace ReportQC.Models;

public class QcFeedbackRequest
{
    /// <summary>关联质控记录 ID</summary>
    public long QcReportId { get; set; }

    /// <summary>关联质控问题 ID</summary>
    public long IssueId { get; set; }

    /// <summary>操作类型：accepted / rejected / modified / supplement</summary>
    public string Action { get; set; } = "accepted";

    /// <summary>医生意见</summary>
    public string? Comment { get; set; }

    /// <summary>修正后的文本</summary>
    public string? CorrectedText { get; set; }

    /// <summary>补充发现的问题</summary>
    public string? SupplementIssues { get; set; }

    /// <summary>操作医生</summary>
    public string? DoctorName { get; set; }
}
