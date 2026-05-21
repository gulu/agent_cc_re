// 术语标准表 TerminologyStandard — 对应 terminology_standard 表
// 标准术语 vs 非标准术语对照，Level 2 语义层使用

using FreeSql.DataAnnotations;

namespace ReportQC.Entities;

public class TerminologyStandard : JSBaseDBEntity
{
    public string StandardTerm { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string NonStandardTerms { get; set; } = string.Empty; // JSON数组
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
