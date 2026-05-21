using FreeSql.DataAnnotations;

namespace Agent_QC.Entities;

public class QcIssue : EntityBase
{
    public long QcReportId { get; set; }
    public int Level { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string? SubType { get; set; }
    public string? OriginalText { get; set; }
    public string? SuggestedText { get; set; }
    public string? Description { get; set; }
    public string Severity { get; set; } = "warning";
    public string? Location { get; set; }
    public string? Suggestion { get; set; }
    public bool? IsAccepted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(nameof(QcReportId))]
    public QcReport? QcReport { get; set; }
}
