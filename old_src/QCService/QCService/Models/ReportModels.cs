// 报告查询相关模型（映射 Oracle v_qc_report 视图）

using System.Text.Json.Serialization;
using FreeSql.DataAnnotations;

namespace QCService.Models;

/// <summary>v_qc_report 视图实体（FreeSQL Oracle 映射）</summary>

public class ViewQcReport
{
    [Column( IsPrimary = true)]
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

/// <summary>报告查询响应 DTO（PascalCase 输出，关键字段与 QCClient ReportInfo 对齐）</summary>
public class ReportQueryResponse
{
    [JsonPropertyName("AccessNumber")]
    public string? AccessNumber { get; set; }

    [JsonPropertyName("PatientName")]
    public string? PatientName { get; set; }

    [JsonPropertyName("PatientGender")]
    public string? PatientGender { get; set; }

    [JsonPropertyName("PatientAge")]
    public string? PatientAge { get; set; }

    [JsonPropertyName("PatientAgeStr")]
    public string? PatientAgeStr => PatientAge;  // QCClient 使用 PatientAgeStr

    [JsonPropertyName("PatientBirthday")]
    public string? PatientBirthday { get; set; }

    [JsonPropertyName("ExamType")]
    public string? ExamType { get; set; }

    [JsonPropertyName("ExamBodyPart")]
    public string? ExamBodyPart { get; set; }

    [JsonPropertyName("ExamDate")]
    public string? ExamDate { get; set; }

    [JsonPropertyName("ClinicalDiagnosis")]
    public string? ClinicalDiagnosis { get; set; }

    [JsonPropertyName("ClinicalHistory")]
    public string? ClinicalHistory { get; set; }

    [JsonPropertyName("ReportContent")]
    public string? ReportContent { get; set; }

    [JsonPropertyName("ReportDiagnosis")]
    public string? ReportDiagnosis { get; set; }

    [JsonPropertyName("ReportDoctor")]
    public string? ReportDoctor { get; set; }

    [JsonPropertyName("ReportDate")]
    public string? ReportDate { get; set; }

    [JsonPropertyName("ReportStatus")]
    public string? ReportStatus { get; set; }

    [JsonPropertyName("Department")]
    public string? Department { get; set; }

    [JsonPropertyName("PatientType")]
    public string? PatientType { get; set; }

    /// <summary>从视图实体映射到响应 DTO</summary>
    public static ReportQueryResponse FromEntity(ViewQcReport entity)
    {
        return new ReportQueryResponse
        {
            AccessNumber = entity.AccessNumber,
            PatientName = entity.PatientName,
            PatientGender = entity.PatientSex,
            PatientAge = entity.PatientAge,
            PatientBirthday = entity.PatientBirthday?.ToString("yyyy-MM-dd"),
            ExamType = entity.ExamType,
            ExamBodyPart = entity.ExamBodyPart,
            ExamDate = entity.ExamDate?.ToString("yyyy-MM-dd"),
            ClinicalDiagnosis = entity.ClinicalDiagnosis,
            ClinicalHistory = entity.ClinicalHistory,
            ReportContent = entity.ReportContent,
            ReportDiagnosis = entity.ReportDiagnosis,
            ReportDoctor = entity.ReportDoctor,
            ReportDate = entity.ReportDate?.ToString("yyyy-MM-dd"),
            ReportStatus = entity.ReportStatus,
            Department = entity.Department,
            PatientType = entity.PatientType
        };
    }
}
