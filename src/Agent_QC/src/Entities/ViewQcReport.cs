using FreeSql.DataAnnotations;

namespace Agent_QC.Entities;

/// <summary>VIEW_QC_REPORT 视图实体（Oracle 映射）</summary>
public class ViewQcReport
{
  
    public string AccessNumber { get; set; } = string.Empty;


    public string? PatientId { get; set; }

  
    public string? PatientName { get; set; }

   
    public string? PatientSex { get; set; }

 
    public string? PatientAge { get; set; }


    public DateTime? PatientBirthday { get; set; }

 
    public string? ExamType { get; set; }

  
    public string? ExamBodyPart { get; set; }

  
    public DateTime? ExamDate { get; set; }


    public DateTime? ExamTime { get; set; }


    public string? ClinicalDiagnosis { get; set; }

    public string? ClinicalHistory { get; set; }


    public string? ReportContent { get; set; }


    public string? ReportDiagnosis { get; set; }


    public string? ReportDoctor { get; set; }


    public DateTime? ReportDate { get; set; }


    public string? ReportStatus { get; set; }


    public string? AuditDoctor { get; set; }


    public DateTime? AuditDate { get; set; }


    public string? Department { get; set; }


    public string? BedNo { get; set; }


    public string? InpatientNo { get; set; }

    public string? OutpatientNo { get; set; }


    public DateTime? ApplicationDate { get; set; }


    public string? PatientType { get; set; }
}
