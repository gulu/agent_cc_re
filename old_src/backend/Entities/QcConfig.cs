// 系统配置表 QcConfig — 对应 qc_config 表
// 质控系统全局配置参数

using FreeSql.DataAnnotations;

namespace ReportQC.Entities;

public class QcConfig : JSBaseDBEntity
{
    public string ConfigKey { get; set; } = string.Empty;
    public string ConfigValue { get; set; } = string.Empty;
    public string? ValueType { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}
