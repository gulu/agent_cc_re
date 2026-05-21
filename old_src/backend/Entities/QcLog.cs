// 系统日志表 QcLog — 对应 qc_log 表
// 记录关键操作日志和异常日志

namespace ReportQC.Entities;

public class QcLog : JSBaseDBEntity
{
    /// <summary>日志级别：ERROR / WARN / INFO / DEBUG</summary>
    public string Level { get; set; } = "INFO";

    /// <summary>日志分类：system / qc / knowledge / api</summary>
    public string Category { get; set; } = "system";

    /// <summary>日志消息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>来源模块/类名</summary>
    public string? Source { get; set; }

    /// <summary>关联报告 ID（可选）</summary>
    public string? ReportId { get; set; }

    /// <summary>异常详情（可选）</summary>
    public string? Exception { get; set; }

    /// <summary>处理耗时（毫秒，可选）</summary>
    public int? DurationMs { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; }
}
