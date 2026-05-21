// RADS 分级标准表 RadsStandard — 对应 rads_standard 表
// BI-RADS/PI-RADS/LI-RADS/TI-RADS 分级标准

using FreeSql.DataAnnotations;

namespace ReportQC.Entities;

public class RadsStandard : JSBaseDBEntity
{
    public string RadsType { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string? GradeName { get; set; }
    public string? Description { get; set; }
    public string? TypicalFindings { get; set; }
    public string? Management { get; set; }
    public string? MalignancyRisk { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
