namespace Agent_QC.Entities;

public class KnowledgeBase : EntityBase
{
    public string CategoryCode { get; set; } = string.Empty;
    public string MatchKey { get; set; } = string.Empty;
    public string MatchValue { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Severity { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
