using FreeSql.DataAnnotations;

namespace Agent_QC.Entities;

public class QcReport : EntityBase
{
    public string ReportId { get; set; } = string.Empty;
    public string Findings { get; set; } = string.Empty;
    public string Impression { get; set; } = string.Empty;
    public string? ReportType { get; set; }
    public string? PatientGender { get; set; }
    public int? PatientAge { get; set; }
    public string? PatientName { get; set; }
    public string? PatientIdNo { get; set; }
    public string? OutpatientNo { get; set; }
    public string? InpatientNo { get; set; }
    public string? PatientPhone { get; set; }
    public string? ClinicalDiagnosis { get; set; }
    public string? Symptoms { get; set; }
    public string? MedicalHistory { get; set; }
    public string? RequestDepartment { get; set; }
    public string? RequestDoctor { get; set; }
    public string? ExamPart { get; set; }
    public string? ExamDevice { get; set; }
    public string? ExamMethod { get; set; }
    public string? RequestNo { get; set; }
    public string? AccessionNo { get; set; }
    public string? ExamDate { get; set; }
    public string? ReportDate { get; set; }
    public decimal? TotalScore { get; set; }
    public decimal? PassScore { get; set; }
    public bool? Passed { get; set; }
    public string? QcLevel { get; set; }
    public int TinyBertTimeMs { get; set; }
    public int RuleEngineTimeMs { get; set; }
    public int TotalTimeMs { get; set; }
    public string? RequestSource { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Navigate(nameof(QcIssue.QcReportId))]
    public List<QcIssue>? Issues { get; set; }

    [Navigate(nameof(ScoreResult.QcReportId))]
    public List<ScoreResult>? ScoreResults { get; set; }
}
