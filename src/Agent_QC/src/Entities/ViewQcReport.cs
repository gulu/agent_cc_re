using FreeSql.DataAnnotations;

namespace Agent_QC.Entities;

/// <summary>VIEW_QC_REPORT 视图实体（Oracle 映射）</summary>
[Table(Name = "VIEW_QC_REPORT")]
public class ViewQcReport
{
    [Column(Name = "ACCESS_NUMBER", IsPrimary = true)]
    public string AccessNumber { get; set; } = string.Empty;

    [Column(Name = "PATIENT_ID")]
    public string? PatientId { get; set; }

    [Column(Name = "PATIENT_NAME")]
    public string? PatientName { get; set; }

    [Column(Name = "PATIENT_SEX")]
    public string? PatientSex { get; set; }

    [Column(Name = "PATIENT_AGE")]
    public string? PatientAge { get; set; }

    [Column(Name = "PATIENT_BIRTHDAY")]
    public DateTime? PatientBirthday { get; set; }

    [Column(Name = "EXAM_TYPE")]
    public string? ExamType { get; set; }

    [Column(Name = "EXAM_BODY_PART")]
    public string? ExamBodyPart { get; set; }

    [Column(Name = "EXAM_DATE")]
    public DateTime? ExamDate { get; set; }

    [Column(Name = "EXAM_TIME")]
    public DateTime? ExamTime { get; set; }

    [Column(Name = "CLINICAL_DIAGNOSIS")]
    public string? ClinicalDiagnosis { get; set; }

    [Column(Name = "CLINICAL_HISTORY")]
    public string? ClinicalHistory { get; set; }

    [Column(Name = "REPORT_CONTENT")]
    public string? ReportContent { get; set; }

    [Column(Name = "REPORT_DIAGNOSIS")]
    public string? ReportDiagnosis { get; set; }

    [Column(Name = "REPORT_DOCTOR")]
    public string? ReportDoctor { get; set; }

    [Column(Name = "REPORT_DATE")]
    public DateTime? ReportDate { get; set; }

    [Column(Name = "REPORT_STATUS")]
    public string? ReportStatus { get; set; }

    [Column(Name = "AUDIT_DOCTOR")]
    public string? AuditDoctor { get; set; }

    [Column(Name = "AUDIT_DATE")]
    public DateTime? AuditDate { get; set; }

    [Column(Name = "DEPARTMENT")]
    public string? Department { get; set; }

    [Column(Name = "BED_NO")]
    public string? BedNo { get; set; }

    [Column(Name = "INPATIENT_NO")]
    public string? InpatientNo { get; set; }

    [Column(Name = "OUTPATIENT_NO")]
    public string? OutpatientNo { get; set; }

    [Column(Name = "APPLICATION_DATE")]
    public DateTime? ApplicationDate { get; set; }

    [Column(Name = "PATIENT_TYPE")]
    public string? PatientType { get; set; }
}
