// 知识库表 KnowledgeBase — 对应 knowledge_base 表
// 规则引擎核心数据源，启动时加载到静态 Dictionary

using FreeSql.DataAnnotations;

namespace ReportQC.Entities;

public class KnowledgeBase : JSBaseDBEntity
{
    public string CategoryCode { get; set; } = string.Empty;
    public string MatchKey { get; set; } = string.Empty;
    public string MatchValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Severity { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
