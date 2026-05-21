namespace Agent_QC.Entities;

public class TerminologyStandard : EntityBase
{
    public string StandardTerm { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? NonStandardTerms { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
