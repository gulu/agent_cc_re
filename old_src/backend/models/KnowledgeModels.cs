// 知识库管理请求 DTO

namespace ReportQC.Models;

/// <summary>知识库条目请求</summary>
public class KnowledgeBaseRequest
{
    public string CategoryCode { get; set; } = string.Empty;
    public string MatchKey { get; set; } = string.Empty;

    /// <summary>匹配值（JSON 数组字符串，如 ["男","未知"]）</summary>
    public string MatchValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Severity { get; set; }
    public int? SortOrder { get; set; }
}

/// <summary>术语标准请求</summary>
public class TerminologyRequest
{
    public string StandardTerm { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    /// <summary>非标准说法列表（JSON 数组字符串）</summary>
    public string NonStandardTerms { get; set; } = "[]";
    public string? Description { get; set; }
}
