namespace Agent_QC.Models;

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

public class QcCheckItem
{
    public string DimensionCode { get; set; } = string.Empty;
    public string DimensionName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public decimal Score { get; set; }
    public decimal Weight { get; set; }
}

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
