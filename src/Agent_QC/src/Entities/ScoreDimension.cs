namespace Agent_QC.Entities;

public class ScoreDimension : EntityBase
{
    public string DimensionCode { get; set; } = string.Empty;
    public string DimensionName { get; set; } = string.Empty;
    public int DefaultWeight { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
