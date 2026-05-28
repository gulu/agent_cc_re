namespace Agent_QC.Models;

public class ReportQueryResponse
{
    public string AccessNumber { get; set; } = string.Empty;
    public string? PatientName { get; set; }
    public string? PatientGender { get; set; }
    public string? PatientAgeStr { get; set; }
    public string? ExamType { get; set; }
    public string? ExamBodyPart { get; set; }
    public string? ClinicalHistory { get; set; }
    public string? Department { get; set; }
    public string? ReportContent { get; set; }
    public string? ReportDiagnosis { get; set; }
}
