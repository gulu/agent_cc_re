namespace Agent_QC.Entities;

public class RadsStandard : EntityBase
{
    public string RadsType { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MalignancyRisk { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
