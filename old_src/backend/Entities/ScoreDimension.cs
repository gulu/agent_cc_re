// 评分维度配置表 ScoreDimension — 对应 score_dimension 表
// 4+N 评分维度的定义与权重

using FreeSql.DataAnnotations;

namespace ReportQC.Entities;

public class ScoreDimension : JSBaseDBEntity
{
    public string DimensionCode { get; set; } = string.Empty;
    public string DimensionName { get; set; } = string.Empty;
    public decimal DefaultWeight { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
